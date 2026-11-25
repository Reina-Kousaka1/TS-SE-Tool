import Config

config :phoenix, :json_library, Jason

config :tsse_runtime, TsseRuntimeWeb.Endpoint,
  adapter: Bandit.PhoenixAdapter,
  server: false,
  render_errors: [formats: [html: TsseRuntimeWeb.ErrorHTML], layout: false],
  pubsub_server: TsseRuntime.PubSub,
  live_view: [signing_salt: "tsse-liveview-salt-v1", layout: {TsseRuntimeWeb.Layouts, :root}]

config :logger, :default_handler, config: [metadata: [:request_id]]

import_config "#{config_env()}.exs"
