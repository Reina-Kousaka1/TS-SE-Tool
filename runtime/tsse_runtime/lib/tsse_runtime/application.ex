defmodule TsseRuntime.Application do
  use Application

  @impl true
  def start(_type, _args) do
    children = [
      {Phoenix.PubSub, name: TsseRuntime.PubSub},
      TsseRuntime.BootstrapToken,
      TsseRuntime.Bridge.Client,
      TsseRuntimeWeb.Endpoint
    ]

    with {:ok, supervisor} <- Supervisor.start_link(children, strategy: :one_for_one, name: TsseRuntime.Supervisor) do
      emit_launch_handshake()
      {:ok, supervisor}
    end
  end

  @impl true
  def config_change(changed, _new, removed) do
    TsseRuntimeWeb.Endpoint.config_change(changed, removed)
    :ok
  end

  defp emit_launch_handshake do
    if System.get_env("TSSE_LAUNCH_HANDSHAKE") == "1" do
      token = TsseRuntime.BootstrapToken.issue_once()
      port = Application.fetch_env!(:tsse_runtime, :runtime_port)
      IO.write(:stdio, Jason.encode!(%{url: "http://127.0.0.1:#{port}/##{token}", ready: true}) <> "\n")
    end
  end
end
