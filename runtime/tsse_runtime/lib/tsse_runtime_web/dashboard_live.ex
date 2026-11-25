defmodule TsseRuntimeWeb.DashboardLive do
  use Phoenix.LiveView

  @impl true
  def mount(_params, _session, socket) do
    send(self(), :load_engine_status)
    {:ok, assign(socket, engine_status: "Querying the local .NET bridge…")}
  end

  @impl true
  def handle_info(:load_engine_status, socket) do
    status =
      case TsseRuntime.Bridge.Client.status() do
        {:ok, response} -> Jason.encode!(response, pretty: true)
        {:error, reason} -> "Bridge unavailable: #{inspect(reason)}"
      end

    {:noreply, assign(socket, :engine_status, status)}
  end

  @impl true
  def render(assigns) do
    ~H"""
    <main>
      <h1>TS-SE-Tool Next Generation</h1>
      <p>Local runtime: active</p>
      <h2>Savegame engine status</h2>
      <pre id="engine-status"><%= @engine_status %></pre>
      <p>Profile listing is not connected. Savegame writes are disabled.</p>
    </main>
    """
  end
end
