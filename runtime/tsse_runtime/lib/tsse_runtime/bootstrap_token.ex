defmodule TsseRuntime.BootstrapToken do
  use GenServer

  def start_link(opts \\ []) do
    case Keyword.get(opts, :name, __MODULE__) do
      nil -> GenServer.start_link(__MODULE__, :ok)
      name -> GenServer.start_link(__MODULE__, :ok, name: name)
    end
  end

  def issue_once(server \\ __MODULE__), do: GenServer.call(server, :issue_once)

  def consume(token, server \\ __MODULE__) when is_binary(token), do: GenServer.call(server, {:consume, token})
  def consume(_, _server), do: :error

  @impl true
  def init(:ok) do
    token = :crypto.strong_rand_bytes(32) |> Base.url_encode64(padding: false)
    {:ok, %{digest: :crypto.hash(:sha256, token), issued: false, consumed: false, initial_token: token}}
  end

  @impl true
  def handle_call(:issue_once, _from, %{issued: false, initial_token: token} = state) do
    {:reply, token, %{state | issued: true, initial_token: nil}}
  end

  def handle_call(:issue_once, _from, state), do: {:reply, nil, state}

  def handle_call({:consume, token}, _from, %{issued: true, consumed: false, digest: digest} = state) do
    candidate = :crypto.hash(:sha256, token)

    if Plug.Crypto.secure_compare(digest, candidate) do
      {:reply, :ok, %{state | consumed: true, digest: nil}}
    else
      {:reply, :error, state}
    end
  end

  def handle_call({:consume, _token}, _from, state), do: {:reply, :error, state}
end
