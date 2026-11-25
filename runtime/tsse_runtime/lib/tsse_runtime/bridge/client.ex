defmodule TsseRuntime.Bridge.Client do
  use GenServer

  @request_limit 16 * 1024
  @timeout_ms 3_000

  def start_link(opts \\ []), do: GenServer.start_link(__MODULE__, :ok, name: Keyword.get(opts, :name, __MODULE__))

  def status, do: GenServer.call(__MODULE__, :status, @timeout_ms + 500)

  @impl true
  def init(:ok) do
    Process.send_after(self(), :retry_open, 1_000)
    {:ok, open_bridge(%{port: nil, buffer: <<>>, executable: System.get_env("TSSE_BRIDGE_EXECUTABLE")})}
  end

  @impl true
  def handle_call(:status, _from, %{port: nil} = state), do: {:reply, {:error, :bridge_unavailable}, state}

  def handle_call(:status, _from, %{port: port} = state) do
    request_id = Base.url_encode64(:crypto.strong_rand_bytes(12), padding: false)
    request = Jason.encode!(%{protocolVersion: 1, requestId: request_id, method: "engine.status"}) <> "\n"

    if Port.command(port, request) do
      case await_response(port, request_id, <<>>, System.monotonic_time(:millisecond) + @timeout_ms) do
        {:ok, response} -> {:reply, {:ok, response}, %{state | buffer: <<>>}}
        {:error, reason} ->
          close_port(port)
          {:reply, {:error, reason}, %{state | port: nil, buffer: <<>>}}
      end
    else
      close_port(port)
      {:reply, {:error, :bridge_unavailable}, %{state | port: nil, buffer: <<>>}}
    end
  end

  @impl true
  def handle_info(:retry_open, state) do
    state = if is_nil(state.port), do: open_bridge(state), else: state
    Process.send_after(self(), :retry_open, 5_000)
    {:noreply, state}
  end

  def handle_info({port, {:exit_status, _status}}, %{port: port} = state) do
    {:noreply, %{state | port: nil, buffer: <<>>}}
  end

  def handle_info(_message, state), do: {:noreply, state}

  @impl true
  def terminate(_reason, %{port: port}) when is_port(port) do
    if Port.info(port), do: Port.close(port)
    :ok
  end

  def terminate(_reason, _state), do: :ok

  defp open_bridge(%{executable: nil} = state), do: state
  defp open_bridge(%{executable: ""} = state), do: state

  defp open_bridge(%{executable: executable} = state) do
    try do
      port = Port.open({:spawn_executable, String.to_charlist(executable)}, [:binary, :use_stdio, :exit_status, :hide, args: []])
      %{state | port: port, buffer: <<>>}
    rescue
      _error -> %{state | port: nil, buffer: <<>>}
    catch
      _kind, _reason -> %{state | port: nil, buffer: <<>>}
    end
  end

  defp await_response(port, request_id, buffer, deadline) do
    remaining = max(deadline - System.monotonic_time(:millisecond), 0)

    receive do
      {^port, {:data, data}} ->
        combined = buffer <> data

        case :binary.match(combined, "\n") do
          {newline, 1} when newline <= @request_limit ->
            line = binary_part(combined, 0, newline) |> String.trim_trailing("\r")

            with {:ok, response} <- Jason.decode(line),
                 ^request_id <- response["requestId"] do
              {:ok, response}
            else
              _ -> {:error, :bridge_protocol_invalid}
            end

          _ when byte_size(combined) > @request_limit -> {:error, :bridge_response_too_large}
          _ -> await_response(port, request_id, combined, deadline)
        end
    after
      remaining -> {:error, :bridge_timeout}
    end
  end

  defp close_port(port) do
    if Port.info(port), do: Port.close(port)
    :ok
  end
end
