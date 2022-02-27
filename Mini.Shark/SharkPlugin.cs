using BepInEx;
using BepInEx.IL2CPP;
using BepInEx.Logging;
using HarmonyLib;
using InnerNet;
using Hazel;
using System;
using System.Text;

namespace Mini.Shark;

[BepInAutoPlugin]
[BepInProcess("Among Us.exe")]
public partial class SharkPlugin : BasePlugin
{
    public Harmony Harmony { get; } = new(Id);

    internal static ManualLogSource Logger;

    public override void Load()
    {
        Logger = base.Log;
        Harmony.PatchAll();
    }

    [HarmonyPatch(typeof(InnerNetClient), nameof(InnerNetClient.SendOrDisconnect))]
    public static class CaptureSentPacketsPatch
    {
        public static void Prefix(InnerNetClient __instance, MessageWriter msg)
        {
            string packet = FormatBytes(msg.Buffer, msg.Length);
            SharkPlugin.Logger.LogInfo($"Sending packet (length={msg.Length}):\n{packet}");
        }
    }

    [HarmonyPatch(typeof(InnerNetClient), nameof(InnerNetClient.OnMessageReceived))]
    public static class CaptureReceivedPacketsPatch
    {
        public static void Prefix(InnerNetClient __instance, DataReceivedEventArgs e)
        {
            var msg = e.Message;
            string packet = FormatBytes(msg.Buffer, msg.Length + msg.Offset);
            SharkPlugin.Logger.LogInfo($"Received packet (length={msg.Length + msg.Offset}):\n{packet}");
        }
    }

    [HarmonyPatch(typeof(InnerNetClient), nameof(InnerNetClient.OnDisconnect))]
    public static class CaptureDisconnectPatch
    {
        public static void Prefix(InnerNetClient __instance, DisconnectedEventArgs e)
        {
            var msg = e.Message;
            if (msg != null ) {
                string packet = FormatBytes(msg.Buffer, msg.Length + msg.Offset);
                SharkPlugin.Logger.LogInfo($"Server disconnected with packet (length={msg.Length + msg.Offset}):\n{packet}");
            } else if (e.Reason != null) {
                SharkPlugin.Logger.LogInfo($"Server disconnected with reason: \"{e.Reason}\"");
            } else {
                SharkPlugin.Logger.LogInfo("Server disconnected without message or reason");
            }
        }
    }

    static string FormatBytes(byte[] array, int length) {
        StringBuilder sb = new StringBuilder();
        int position = 0;
        // Most hazel buffers are very large, but the disconnect buffer is very small. Prevent indexing too far into the array
        int actualLength = Math.Min(length, array.Length);
        while (position < length) {
            sb.Append(position.ToString("X8"));
            sb.Append(": ");

            for (int i = 0; i < 16; i++) {
                if (position + i < actualLength) {
                    sb.Append(array[position + i].ToString("X2"));
                } else if (position + i < length) {
                    // Provided buffer is shorter than the declared length.
                    sb.Append("??");
                } else {
                    sb.Append("  ");
                }
                if (i % 2 == 1) {
                    sb.Append(' ');
                }
            }
            sb.Append(' ');

            for (int i = 0; i < 16; i++) {
                if (position + i < actualLength) {
                    char c = (char) array[position + i];
                    if (Char.IsControl(c) || c >= 0x80) {
                        c = '.';
                    }
                    sb.Append(c);

                }
            }

            sb.Append('\n');
            position += 16;
        }
        return sb.ToString();
    }
}
