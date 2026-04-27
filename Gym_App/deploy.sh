#!/bin/bash
cd "Gym_App/bin/Release/net10.0-android"
adb uninstall com.leiyu.GymJournal >/dev/null 2>&1 || true
adb install com.leiyu.GymJournal-Signed.apk || (echo "Install failed, trying with cache trim..." && adb shell pm trim-caches 100M && adb install com.leiyu.GymJournal-Signed.apk)