defmodule TsseRuntimeWeb.Endpoint do
  use Phoenix.Endpoint, otp_app: :tsse_runtime

  @session_options [
    store: :cookie,
    key: "_tsse_local_session",
    signing_salt: "tsse-session-sign-v1",
    encryption_salt: "tsse-session-encrypt-v1",
    same_site: "Strict",
    http_only: true,
    secure: false,
    max_age: 3_600
  ]

  socket "/live", Phoenix.LiveView.Socket,
    websocket: [connect_info: [session: @session_options]]

  plug Plug.RequestId
  plug Plug.Telemetry, event_prefix: [:phoenix, :endpoint]
  plug TsseRuntimeWeb.OriginGuard
  plug TsseRuntimeWeb.SecurityHeaders
  plug Plug.Static, at: "/", from: :tsse_runtime, gzip: false, only: ~w(assets favicon.ico robots.txt)
  plug Plug.Parsers,
    parsers: [:urlencoded, :multipart, :json],
    pass: ["*/*"],
    json_decoder: Phoenix.json_library(),
    length: 16_384
  plug Plug.MethodOverride
  plug Plug.Head
  plug Plug.Session, @session_options
  plug TsseRuntimeWeb.Router
end
