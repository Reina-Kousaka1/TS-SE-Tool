import os
import subprocess
import sys
import time


app_path = os.environ.get("TSSE_APP_PATH")
if not app_path:
    sys.exit("TSSE_APP_PATH must identify the desktop executable.")

env = os.environ.copy()
env["TSSE_DESKTOP_CI_SMOKE_TEST"] = "1"
screenshot_path = env.get("TSSE_DESKTOP_SCREENSHOT_PATH")
if screenshot_path:
    env["TSSE_DESKTOP_CI_SCREENSHOT"] = "1"
process = subprocess.Popen([app_path], env=env)


def terminate_process():
    if process.poll() is not None:
        return
    process.terminate()
    try:
        process.wait(timeout=5)
    except subprocess.TimeoutExpired:
        process.kill()
        process.wait()


try:
    if screenshot_path:
        deadline = time.monotonic() + 15
        window_id = None
        while time.monotonic() < deadline:
            if process.poll() is not None:
                raise RuntimeError(
                    f"The Avalonia desktop process exited before its window appeared (code {process.returncode}).")
            search = subprocess.run(
                ["xdotool", "search", "--onlyvisible", "--name", "^TS-SE Tool"],
                check=False,
                capture_output=True,
                text=True,
                timeout=5,
            )
            window_ids = search.stdout.splitlines()
            if search.returncode == 0 and window_ids:
                window_id = window_ids[-1]
                break
            time.sleep(0.2)

        if window_id is None:
            raise RuntimeError("The native Avalonia window was not visible on the Xvfb display.")

        os.makedirs(os.path.dirname(screenshot_path), exist_ok=True)
        capture = subprocess.run(
            ["import", "-window", window_id, screenshot_path],
            check=False,
            capture_output=True,
            text=True,
            timeout=15,
        )
        if capture.returncode != 0:
            raise RuntimeError(f"Native window screenshot failed: {capture.stderr.strip()}")
        with open(screenshot_path, "rb") as screenshot:
            if screenshot.read(8) != b"\x89PNG\r\n\x1a\n":
                raise RuntimeError("Native window screenshot was not a valid PNG.")
        inspection = subprocess.run(
            ["identify", "-format", "%w %h %k", screenshot_path],
            check=False,
            capture_output=True,
            text=True,
            timeout=5,
        )
        if inspection.returncode != 0:
            raise RuntimeError(f"Native window screenshot inspection failed: {inspection.stderr.strip()}")
        try:
            width, height, color_count = map(int, inspection.stdout.split())
        except ValueError as error:
            raise RuntimeError("Native window screenshot inspection returned invalid dimensions.") from error
        if width < 820 or height < 640 or color_count < 16:
            raise RuntimeError(
                f"Native window screenshot is unexpectedly blank or small: {width}x{height}, {color_count} colors.")
        print(
            f"Captured native Avalonia window at {width}x{height} with {color_count} colors: {screenshot_path}.")

    exit_code = process.wait(timeout=25)
except subprocess.TimeoutExpired:
    terminate_process()
    sys.exit("The Avalonia desktop process did not close after its window startup smoke test.")
except Exception as error:
    terminate_process()
    sys.exit(f"Native desktop smoke test failed: {error}")

if exit_code != 0:
    sys.exit(f"The Avalonia desktop process exited with code {exit_code}.")
print("The Avalonia desktop process opened and closed cleanly.")
