import Config

config :tsse_runtime, TsseRuntimeWeb.Endpoint,
  server: false,
  check_origin: false

config :logger, level: :warning
