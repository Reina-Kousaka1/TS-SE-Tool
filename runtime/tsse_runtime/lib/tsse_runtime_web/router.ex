defmodule TsseRuntimeWeb.Router do
  use Phoenix.Router
  import Phoenix.LiveView.Router

  pipeline :local_browser do
    plug :accepts, ["html", "json"]
    plug :fetch_session
    plug :protect_from_forgery
  end

  scope "/", TsseRuntimeWeb do
    pipe_through :local_browser

    get "/", BootstrapController, :index
    post "/bootstrap", BootstrapController, :create

    live_session :authenticated, on_mount: [{TsseRuntimeWeb.AuthHook, :require_authenticated}] do
      live "/dashboard", DashboardLive
    end
  end
end
