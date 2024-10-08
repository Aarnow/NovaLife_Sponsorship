﻿using Life;
using Life.DB;
using Life.Network;
using Life.UI;
using ModKit.Helper;
using ModKit.Interfaces;
using ModKit.Utils;
using Socket.Newtonsoft.Json;
using Sponsorship.Classes;
using Sponsorship.Entities;
using Sponsorship.Points;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Text.RegularExpressions;
using _menu = AAMenu.Menu;
using mk = ModKit.Helper.TextFormattingHelper;

namespace Sponsorship
{
    public class Sponsorship : ModKit.ModKit
    {
        bool hodor = false;

        public static string ConfigDirectoryPath;
        public static string ConfigSponsorshipPath;
        public static SponsorshipConfig _sponsorshipConfig;

        public Sponsorship(IGameAPI api) : base(api)
        {
            PluginInformations = new PluginInformations(AssemblyHelper.GetName(), "1.0.0", "Aarnow");
        }

        public override void OnPluginInit()
        {
            base.OnPluginInit();
            InitConfig();
            _sponsorshipConfig = LoadConfigFile(ConfigSponsorshipPath);

            Orm.RegisterTable<Sponsorship_Player>();
            Orm.RegisterTable<Sponsorship_Reward>();

            Orm.RegisterTable<Sponsorship_Point>();
            PointHelper.AddPattern("Sponsorship_Point", new Sponsorship_Point(false));
            AAMenu.AAMenu.menu.AddBuilder(PluginInformations, "Sponsorship_Point", new Sponsorship_Point(false), this);

            InsertMenu();

            ModKit.Internal.Logger.LogSuccess($"{PluginInformations.SourceName} v{PluginInformations.Version}", "initialisé");
        }

        public async override void OnPlayerSpawnCharacter(Player player, Mirror.NetworkConnection conn, Characters character)
        {
            base.OnPlayerSpawnCharacter(player, conn, character);

            if(!hodor) NetworkAreaHelper.SetServerDoorState("110d8269-1106-4c37-bc54-b15c598cd989-1288", true, true);

            var steamId = player.steamId.ToString();
            List<Sponsorship_Player> currentPlayer = await Sponsorship_Player.Query(p => p.PlayerSteamId == steamId);
            if(currentPlayer != null && currentPlayer.Count > 0 && currentPlayer[0] != null)
            {
                int currentDate = DateUtils.GetNumericalDateOfTheDay();
                if (currentPlayer[0].LastConnection != currentDate)
                {
                    currentPlayer[0].LastConnection = currentDate;
                    currentPlayer[0].ConnectionCount += 1;
                    await currentPlayer[0].Save();
                }
            }
        }

        #region Config
        private void InitConfig()
        {
            try
            {
                ConfigDirectoryPath = DirectoryPath + "/Sponsorship";
                ConfigSponsorshipPath = Path.Combine(ConfigDirectoryPath, "sponsorshipConfig.json");

                if (!Directory.Exists(ConfigDirectoryPath)) Directory.CreateDirectory(ConfigDirectoryPath);
                if (!File.Exists(ConfigSponsorshipPath)) InitSponsorshipConfig();
            }
            catch (IOException ex)
            {
                ModKit.Internal.Logger.LogError("InitDirectory", ex.Message);
            }
        }

        private void InitSponsorshipConfig()
        {
            SponsorshipConfig sponsorshipConfig = new SponsorshipConfig();
            string json = JsonConvert.SerializeObject(sponsorshipConfig);
            File.WriteAllText(ConfigSponsorshipPath, json);
        }

        private SponsorshipConfig LoadConfigFile(string path)
        {
            if (File.Exists(path))
            {
                string jsonContent = File.ReadAllText(path);
                SponsorshipConfig sponsorshipConfig = JsonConvert.DeserializeObject<SponsorshipConfig>(jsonContent);

                return sponsorshipConfig;
            }
            else return null;
        }
        #endregion

        public void InsertMenu()
        {
            _menu.AddAdminPluginTabLine(PluginInformations, 5, "Sponsorship", (ui) =>
            {
                Player player = PanelHelper.ReturnPlayerFromPanel(ui);
                SponsorshipPanel(player);
            });
        }

        #region PANELS
        public void SponsorshipPanel(Player player)
        {
            //Déclaration
            Panel panel = PanelHelper.Create("Sponsorship", UIPanel.PanelType.TabPrice, player, () => SponsorshipPanel(player));

            //Corps
            panel.AddTabLine("Liste des récompenses", _ => SponsorshipPanelReward(player));
            panel.AddTabLine($"{mk.Color("Appliquer la configuration", mk.Colors.Info)}", _ =>
            {
                _sponsorshipConfig = LoadConfigFile(ConfigSponsorshipPath);
                panel.Refresh();
            });

            panel.NextButton("Sélectionner", () => panel.SelectTab());
            panel.AddButton("Retour", _ => AAMenu.AAMenu.menu.AdminPluginPanel(player));
            panel.CloseButton();

            //Affichage
            panel.Display();
        }

        public async void SponsorshipPanelReward(Player player)
        {
            //Query
            List<Sponsorship_Reward> rewards = await Sponsorship_Reward.QueryAll();

            //Déclaration
            Panel panel = PanelHelper.Create("Sponsorship - Récompenses", UIPanel.PanelType.TabPrice, player, () => SponsorshipPanelReward(player));

            //Corps
            if (rewards.Count > 0)
            {
                foreach (var reward in rewards)
                {
                    var currentItem = ItemUtils.GetItemById(reward.ItemId);
                    panel.AddTabLine($"{currentItem.itemName} x {reward.ItemQuantity}", $"Prestige requis: {reward.MenteeRequired}", ItemUtils.GetIconIdByItemId(currentItem.id), _ =>
                    {
                        SponsorshipPanelRewardDetails(player, reward);
                    });
                }
            }
            else panel.AddTabLine("Aucune récompense", _ => { });


            panel.NextButton("Ajouter", () => SponsorshipPanelRewardDetails(player));
            if (rewards.Count > 0) panel.NextButton("Modifier", () => panel.SelectTab());
            panel.PreviousButton();
            panel.CloseButton();

            //Affichage
            panel.Display();
        }
        public void SponsorshipPanelRewardDetails(Player player, Sponsorship_Reward reward = null)
        {
            if (reward == null) reward = new Sponsorship_Reward();

            //Déclaration
            Panel panel = PanelHelper.Create($"Sponsorship - Gestion d'une récompense", UIPanel.PanelType.TabPrice, player, () => SponsorshipPanelRewardDetails(player, reward));

            //Corps
            panel.AddTabLine($"{mk.Color("Récompense:", mk.Colors.Info)} {(reward.ItemId != default ? $"{ItemUtils.GetItemById(reward.ItemId).itemName} x {reward.ItemQuantity}" : "à définir")}", _ => RewardSetItem(player, reward));
            panel.AddTabLine($"{mk.Color("Filleuls requis:", mk.Colors.Info)} {reward.MenteeRequired}", _ => RewardSetMenteeRequired(player, reward));


            panel.NextButton("Sélectionner", () => panel.SelectTab());
            if (reward.Id != default)
            {
                panel.PreviousButtonWithAction("Supprimer", async () =>
                {
                    if (await reward.Delete())
                    {
                        player.Notify("Sponsorship", "Récompense supprimée", NotificationManager.Type.Success);
                        return true;
                    }
                    else
                    {
                        player.Notify("Sponsorship", "Nous n'avons pas pu supprimer cette récompense", NotificationManager.Type.Error);
                        return false;
                    }
                });
            }
            panel.PreviousButton();
            panel.CloseButton();

            //Affichage
            panel.Display();
        }

        #region SETTERS
        public void RewardSetItem(Player player, Sponsorship_Reward reward)
        {
            //Déclaration
            Panel panel = PanelHelper.Create($"Sponsorship - Récompense", UIPanel.PanelType.Input, player, () => RewardSetItem(player, reward));

            //Corps
            panel.TextLines.Add("Définir l'ID de l'objet et le nombre à offrir en récompense");
            panel.inputPlaceholder = "[ID] [QUANTITÉ]";

            //Boutons
            panel.PreviousButtonWithAction("Sauvegarder", async () =>
            {
                string pattern = @"^(\d+)\s(\d+)$";
                Match match = Regex.Match(panel.inputText, pattern);

                if (match.Success)
                {
                    var currentItem = ItemUtils.GetItemById(int.Parse(match.Groups[1].Value));

                    if (currentItem != null)
                    {
                        var quantity = int.Parse(match.Groups[2].Value);

                        if (quantity > 0)
                        {
                            reward.ItemId = currentItem.id;
                            reward.ItemQuantity = quantity;

                            if (await reward.Save())
                            {
                                player.Notify("Sponsorship", "récompense enregistrée", NotificationManager.Type.Success);
                                return true;
                            }
                            else
                            {
                                player.Notify("Sponsorship", "Nous n'avons pas pu enregistrer votre récompense", NotificationManager.Type.Error);
                                return false;
                            }
                        }
                        else
                        {
                            player.Notify("Sponsorship", "La quantité à fournir doit être supérieure", NotificationManager.Type.Warning);
                            return false;
                        }
                    }
                    else
                    {
                        player.Notify("Sponsorship", "L'objet renseigné n'existe pas", NotificationManager.Type.Warning);
                        return false;
                    }
                }
                else
                {
                    player.Notify("Sponsorship", "Format incorrect", NotificationManager.Type.Warning);
                    return false;
                }
            });
            panel.PreviousButton();
            panel.CloseButton();

            //Affichage
            panel.Display();
        }
        public void RewardSetMenteeRequired(Player player, Sponsorship_Reward reward)
        {
            //Déclaration
            Panel panel = PanelHelper.Create($"Sponsorship - Filleuls requis", UIPanel.PanelType.Input, player, () => RewardSetMenteeRequired(player, reward));

            //Corps
            panel.TextLines.Add("Combien faut-il de filleuls confirmés pour récupérer la récompense");
            panel.inputPlaceholder = "exemple: 10";

            //Boutons
            panel.PreviousButtonWithAction("Sauvegarder", async () =>
            {
                if (int.TryParse(panel.inputText, out int prestigeRequired))
                {
                    if (prestigeRequired > 0)
                    {
                        reward.MenteeRequired = prestigeRequired;

                        if (await reward.Save())
                        {
                            player.Notify("Sponsorship", "récompense enregistrée", NotificationManager.Type.Success);
                            return true;
                        }
                        else
                        {
                            player.Notify("Sponsorship", "Nous n'avons pas pu enregistrer votre récompense", NotificationManager.Type.Error);
                            return false;
                        }
                    }
                    else
                    {
                        player.Notify("Sponsorship", "Renseigner une valeur positive", NotificationManager.Type.Warning);
                        return false;
                    }
                }
                else
                {
                    player.Notify("Sponsorship", "Format incorrect", NotificationManager.Type.Warning);
                    return false;
                }
            });
            panel.PreviousButton();
            panel.CloseButton();

            //Affichage
            panel.Display();
        }
        #endregion
        #endregion
    }
}
