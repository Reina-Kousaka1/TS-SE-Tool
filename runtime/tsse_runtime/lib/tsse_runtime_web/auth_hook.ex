defmodule TsseRuntimeWeb.AuthHook do
  import Phoenix.LiveView

  def on_mount(:require_authenticated, _params, %{"authenticated" => true}, socket),
    do: {:cont, socket}

  def on_mount(:require_authenticated, _params, _session, socket),
    do: {:halt, redirect(socket, to: "/")}
end
