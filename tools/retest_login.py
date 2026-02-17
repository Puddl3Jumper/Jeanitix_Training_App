import subprocess
import time
import re
import xml.etree.ElementTree as ET

ADB = "/Users/lei.yu/Library/Android/sdk/platform-tools/adb"
PKG = "com.leiyu.GymJournal"


def sh(*args: str) -> str:
    return subprocess.run([ADB, *args], stdout=subprocess.PIPE, stderr=subprocess.STDOUT, text=True, check=False).stdout


def dump_xml() -> ET.Element:
    sh("shell", "uiautomator", "dump", "/sdcard/window_dump.xml")
    xml_text = sh("shell", "cat", "/sdcard/window_dump.xml")
    return ET.fromstring(xml_text)


def tap_text(text: str):
    root = dump_xml()
    for node in root.iter("node"):
        if node.attrib.get("text", "") == text:
            bounds = node.attrib.get("bounds", "")
            match = re.match(r"\[(\d+),(\d+)\]\[(\d+),(\d+)\]", bounds)
            if not match:
                continue
            x1, y1, x2, y2 = map(int, match.groups())
            x = (x1 + x2) // 2
            y = (y1 + y2) // 2
            sh("shell", "input", "tap", str(x), str(y))
            return True, bounds
    return False, ""


def main():
    sh("logcat", "-c")
    sh("shell", "am", "force-stop", PKG)
    sh("shell", "pm", "clear", PKG)
    sh("shell", "monkey", "-p", PKG, "-c", "android.intent.category.LAUNCHER", "1")
    time.sleep(4)

    ok_continue, bounds_continue = tap_text("I Already have an Account")
    time.sleep(2)
    ok_google, bounds_google = tap_text("Continue with Google")
    time.sleep(4)

    focus = sh("shell", "dumpsys", "window")
    focus_lines = [ln.strip() for ln in focus.splitlines() if "mCurrentFocus" in ln or "mFocusedApp" in ln]

    # Keep this small: only the tags we care about.
    filtered_text = sh(
        "logcat",
        "-d",
        "-v",
        "brief",
        "OAuth2:V",
        "AndroidRuntime:E",
        "ActivityTaskManager:I",
        "monodroid:E",
        "monodroid-assembly:E",
        "*:S",
    )

    print("tap_continue", ok_continue, bounds_continue)
    print("tap_google", ok_google, bounds_google)
    print("focus")
    for ln in focus_lines[-2:]:
        print(ln)

    print("log_tail")
    lines = [ln for ln in filtered_text.splitlines() if ln.strip()]
    for ln in lines[-120:]:
        print(ln)


if __name__ == "__main__":
    main()
