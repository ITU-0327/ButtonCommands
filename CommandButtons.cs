using System;
using System.Collections.Generic;
using System.Linq;
using Network;
using Newtonsoft.Json;
using Oxide.Core;
using Oxide.Game.Rust.Cui;
using UnityEngine;

namespace Oxide.Plugins
{
    [Info("Command Buttons", "ITU", "1.0.0")]
    [Description("Create your own GUI buttons for commands.")]
    class CommandButtons : RustPlugin
    {
        #region Variables

        private static CuiPanel Menu;

        #endregion
        

        #region Configuration

        private Configuration _config;

        private class Configuration
        {
            [JsonProperty(PropertyName = "Background Color")]
            public string backgroundColor = "0 0 0 0.8";

            [JsonProperty(PropertyName = "GUI Left Top Position")]
            public string LeftTopPosition = "0.01 0.88";

            [JsonProperty(PropertyName = "Distance between buttons(horizontal)")]
            public float HorizontalBetweenButtons = 0.002f;

            [JsonProperty(PropertyName = "Distance between buttons(vertical)")]
            public float VerticalBetweenButtons = 0.004f;

            [JsonProperty(PropertyName = "Button width")]
            public float ButtonWidth = 0.085f;

            [JsonProperty(PropertyName = "Button height")]
            public float ButtonHeight = 0.035f;
            
            [JsonProperty(PropertyName = "List of buttons", ObjectCreationHandling = ObjectCreationHandling.Replace)]
            public List<ConfigButton> Buttons = new List<ConfigButton> { new ConfigButton() };

        }

        private class ConfigButton
        {
            [JsonIgnore] public CuiButton Button;
            
            [JsonProperty(PropertyName = "Button color")]
            public string ButtonColor = "0.0 0.0 0.0 1.0";

            [JsonProperty(PropertyName = "Text color")]
            public string TextColor = "#ffffff";

            [JsonProperty(PropertyName = "Text size")]
            public short TextSize = 12;

            [JsonProperty(PropertyName = "Button text")]
            public string Text = "Accept TP";

            [JsonProperty(PropertyName = "Execute chat (true) or console (false) command")]
            public bool IsChatCommand = true;

            [JsonProperty(PropertyName = "Executing command")]
            public string Command = "/tpa";
        }

        protected override void LoadConfig()
        {
            base.LoadConfig();
            try
            {
                _config = Config.ReadObject<Configuration>();
                if (_config == null) throw new Exception();
            }
            catch
            {
                PrintError("Your configuration file contains an error. Using default configuration values.");
                LoadDefaultConfig();
            }
        }

        protected override void SaveConfig() => Config.WriteObject(_config);

        protected override void LoadDefaultConfig() => _config = new Configuration();

        #endregion




        // HOOKS
        #region Hooks

        private void Init()
        {
            LoadConfig();
            
            cmd.AddConsoleCommand("commandbuttons.exec", this, arg =>
            {
                if (!arg.HasArgs(2))
                    return false;

                var isChat = arg.Args[0] == "chat";
                SendCommand(arg.Connection, arg.Args.Skip(1).ToArray(), isChat);
                
                return false;
            });
            
            // Loading CUIs
            var buttonsCount = _config.Buttons.Count;            

            var minGuiMarginHorizontal = _config.HorizontalBetweenButtons;
            var minGuiMarginVertical = _config.VerticalBetweenButtons;
            var minGuiWidth = _config.ButtonWidth;
            var minGuiHeight = _config.ButtonHeight;

            var backgroundWidth = 2 * minGuiMarginHorizontal + minGuiWidth;
            var backgroundHeight = minGuiMarginVertical + buttonsCount * (minGuiHeight + minGuiMarginVertical);
            var relativeButtonWidth = minGuiWidth / backgroundWidth;
            var relativeButtonHeight = minGuiHeight / backgroundHeight;
            var relativeMarginHorizontal = minGuiMarginHorizontal / backgroundWidth;
            var relativeMarginVertical = minGuiMarginVertical / backgroundHeight;
            
            // Loading menu
            string[] LeftTopCorner = _config.LeftTopPosition.Split(' ');
            var mleft = double.Parse(LeftTopCorner[0]);
            var mtop = double.Parse(LeftTopCorner[1]);
            var mright = mleft + backgroundWidth;
            var mbutton = mtop - backgroundHeight;
            
            Menu = new CuiPanel
            {
                Image =
                {
                    Color = _config.backgroundColor
                },
                CursorEnabled = false,
                RectTransform =
                {
                    AnchorMin = $"{mleft} {mbottom}",
                    AnchorMax = $"{mright} {mtop}"
                }
            };


            // Loading buttons
            for (var i = 0; i < buttonsCount; i++)
            {
                var button = _config.Buttons[i];

                var left =  relativeMarginHorizontal;
                var top = 1 - relativeMarginVertical - i * (relativeButtonHeight + relativeMarginVertical);
                var bottom = top - relativeButtonHeight;
                var right = left + relativeButtonWidth;
                
                var type = button.IsChatCommand ? "chat" : "console";

                button.Button = new CuiButton
                {
                    Text =
                    {
                        Text = $"<color={button.TextColor}>{button.Text}</color>",
                        FontSize = button.TextSize,
                        Align = TextAnchor.MiddleCenter,
                    },
                    Button =
                    {
                        Color = button.ButtonColor,
                        Command = $"commandbuttons.exec {type} {button.Command}",
                    },
                    RectTransform =
                    {
                        AnchorMin = $"{left} {bottom}",
                        AnchorMax = $"{right} {top}"
                    }
                };
            }

            var playersCount = BasePlayer.activePlayerList.Count;
            for (var i = 0; i < playersCount; i++)
                ShowUI(BasePlayer.activePlayerList[i], _config);
        }

        private void Unload()
        {
            foreach (var player in BasePlayer.activePlayerList)
            {
                DestroyUI(player);
            }
        }
        
        private void OnPlayerSleepEnded(BasePlayer player)
        {
            ShowUI(player, _config);
        }

        private void OnPlayerDeath(BasePlayer player)
        {
            DestroyUI(player);
        }

        protected override void LoadDefaultMessages()
        {
            lang.RegisterMessages(new Dictionary<string, string>
            {
                {"No Permission", "You don't have enough permission to run this command!"},
                {"Only Player", "This command can be used only by players!"}
            }, this);
            
            lang.RegisterMessages(new Dictionary<string, string>
            {
                {"No Permission", "你没有权限使用这个指令!"},
                {"Only Player", "这个指令只能由玩家使用!"}
            }, this, "zh-CN");
        }

        #endregion


        //Helpers
        #region Helpers

        private void ShowUI(BasePlayer player, Configuration config)
        {
            var GUIElement = new CuiElementContainer();
            GUIElement.Add(Menu, "Hud", "GameMenuCUI");

            var buttonsCount = config.Buttons.Count;
            for (var i = 0; i < buttonsCount; i++)
                GUIElement.Add(config.Buttons[i].Button, "GameMenuCUI", "GameMenuCUIButton");

            DestroyUI(player);
            CuiHelper.AddUi(player, GUIElement);
        }

        private void DestroyUI(BasePlayer player)
        {
            CuiHelper.DestroyUi(player, "GameMenuCUI");
        }

        private void SendCommand(Connection conn, string[] args, bool isChat)
        {
            if (!Net.sv.IsConnected())
                return;

            var command = string.Empty;
            var argsLength = args.Length;
            for (var i = 0; i < argsLength; i++)
                command += $"{args[i]} ";
            
            if (isChat)
                command = $"chat.say {command.QuoteSafe()}";
            
            Net.sv.write.Start();
            Net.sv.write.PacketID(Message.Type.ConsoleCommand);
            Net.sv.write.String(command);
            Net.sv.write.Send(new SendInfo(conn));
        }

        private bool CanUse(BasePlayer player, string perm) =>
            player.IsAdmin || string.IsNullOrEmpty(perm) || permission.UserHasPermission(player.UserIDString, perm);

        private string GetMsg(string key, string userId = null) => lang.GetMessage(key, this, userId);

        #endregion
    }
}