defmodule TsseRuntimeWeb.SecurityHeaders do
  import Plug.Conn

  def init(opts), do: opts

  def call(conn, _opts) do
    port = Application.fetch_env!(:tsse_runtime, :runtime_port)
    policy = "default-src 'self'; script-src 'self'; style-src 'self'; img-src 'self' data:; connect-src 'self' ws://127.0.0.1:#{port}; object-src 'none'; base-uri 'none'; frame-ancestors 'none'; form-action 'self'"

    conn
    |> put_resp_header("content-security-policy", policy)
    |> put_resp_header("x-content-type-options", "nosniff")
    |> put_resp_header("referrer-policy", "no-referrer")
    |> put_resp_header("x-frame-options", "DENY")
    |> put_resp_header("cross-origin-resource-policy", "same-origin")
    |> put_resp_header("cache-control", "no-store")
  end
end
