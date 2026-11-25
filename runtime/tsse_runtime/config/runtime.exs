import Config

port =
  case Integer.parse(System.get_env("TSSE_RUNTIME_PORT", "41739")) do
    {value, ""} when value in 1024..65535 -> value
    _ -> raise "TSSE_RUNTIME_PORT must be an integer from 1024 through 65535"
  end

secret_key_base = System.get_env("TSSE_SECRET_KEY_BASE")

secret_key_base =
  if config_env() == :prod do
    if is_nil(secret_key_base) or byte_size(secret_key_base) < 64 do
      raise "Production requires a locally provisioned TSSE_SECRET_KEY_BASE of at least 64 bytes"
    end

    secret_key_base
  else
    secret_key_base || Base.url_encode64(:crypto.strong_rand_bytes(64), padding: false)
  end

origin_check = {TsseRuntimeWeb.OriginGuard, :websocket_origin?, [port]}

config :tsse_runtime, :runtime_port, port

config :tsse_runtime, TsseRuntimeWeb.Endpoint,
  url: [scheme: "http", host: "127.0.0.1", port: port],
  http: [ip: {127, 0, 0, 1}, port: port],
  secret_key_base: secret_key_base,
  check_origin: origin_check

if config_env() == :prod do
  if is_nil(System.get_env("TSSE_BRIDGE_EXECUTABLE")) do
    raise "Production requires TSSE_BRIDGE_EXECUTABLE to point to the packaged bridge executable"
  end
end
