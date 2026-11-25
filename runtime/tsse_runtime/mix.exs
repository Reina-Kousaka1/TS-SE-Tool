defmodule TsseRuntime.MixProject do
  use Mix.Project

  def project do
    [
      app: :tsse_runtime,
      version: "0.1.0",
      elixir: "~> 1.18",
      start_permanent: Mix.env() == :prod,
      deps: deps(),
      aliases: aliases()
    ]
  end

  def application do
    [
      mod: {TsseRuntime.Application, []},
      extra_applications: [:logger, :runtime_tools, :crypto]
    ]
  end

  defp deps do
    [
      {:bandit, "== 1.14.0"},
      {:jason, "== 1.4.5"},
      {:phoenix, "== 1.8.17"},
      {:phoenix_html, "== 4.4.0"},
      {:phoenix_live_view, "== 1.2.15"}
    ]
  end

  defp aliases do
    ["assets.build": ["cmd npm run assets:build"]]
  end
end
