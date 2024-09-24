using Life.Network;
using Life.UI;
using SQLite;
using System.Threading.Tasks;
using ModKit.Helper;
using ModKit.Helper.PointHelper;
using mk = ModKit.Helper.TextFormattingHelper;
using System.Collections.Generic;
using System.Linq;
using Life;
using Sponsorship.Entities;
using ModKit.Utils;
using Life.InventorySystem;

namespace Sponsorship.Points
{
    internal class Sponsorship_Point : ModKit.ORM.ModEntity<Sponsorship_Point>, PatternData
    {
        [AutoIncrement][PrimaryKey] public int Id { get; set; }
        public string TypeName { get; set; }
        public string PatternName { get; set; }

        //Declare your other properties here

        [Ignore] public ModKit.ModKit Context { get; set; }

        public Sponsorship_Point() { }
        public Sponsorship_Point(bool isCreated)
        {
            TypeName = nameof(Sponsorship_Point);
        }

        /// <summary>
        /// Applies the properties retrieved from the database during the generation of a point in the game using this model.
        /// </summary>
        /// <param name="patternId">The identifier of the pattern in the database.</param>
        public async Task SetProperties(int patternId)
        {
            var result = await Query(patternId);

            Id = patternId;
            TypeName = nameof(Sponsorship_Point);
            PatternName = result.PatternName;
        }

        /// <summary>
        /// Contains the action to perform when a player interacts with the point.
        /// </summary>
        /// <param name="player">The player interacting with the point.</param>
        public void OnPlayerTrigger(Player player)
        {
            SponsorshipPanel(player);
        }

        #region CUSTOM
        public async void SponsorshipPanel(Player player)
        {
            //Query
            string steamId = player.steamId.ToString();
            List<Sponsorship_Player> currentPlayer = await Sponsorship_Player.Query(p => p.PlayerSteamId == steamId);

            //Déclaration
            Panel panel = Context.PanelHelper.Create("Parrainage", UIPanel.PanelType.Text, player, () => SponsorshipPanel(player));

            //Corps & Boutons
            if (currentPlayer != null && currentPlayer.Count > 0)
            {
                currentPlayer[0].LRewardRecovered = ListConverter.ReadJson(currentPlayer[0].RewardRecovered);
                currentPlayer[0].LMenteePlayers = ListConverter.ReadJson(currentPlayer[0].MenteePlayers);

                panel.TextLines.Add($"Bonjour {mk.Color(player.GetFullName(), mk.Colors.Orange)}"); //Parler du systeme de parrainage
                panel.TextLines.Add($"{(currentPlayer[0].MentorId != default ? $"Vous êtes parrainé par {mk.Color(currentPlayer[0].MentorFullName, mk.Colors.Info)}" : $"Vous n'avez {mk.Color("pas de parrain", mk.Colors.Info)}")}");
                panel.TextLines.Add($"Vous avez {mk.Color($"{currentPlayer[0].LMenteePlayers.Count} filleul{(currentPlayer[0].LMenteePlayers.Count > 1 ? "s" : "")}", mk.Colors.Info)}");

                panel.NextButton("Parrain", () => SponsorshipMentor(player, currentPlayer[0]));
                panel.NextButton("Filleuls", () => SponsorshipMentee(player, currentPlayer[0]));
                panel.NextButton("Récompenses", () => SponsorshipReward(player, currentPlayer[0]));
            }
            else
            {
                panel.TextLines.Add($"Bienvenue en ville !"); //Parler du systeme de parrainage
                panel.TextLines.Add("Inscrivez-vous pour parrainer vos amis et définir votre parrain !");

                panel.NextButton("S'inscrire", async () =>
                {
                    Sponsorship_Player newSponsor = new Sponsorship_Player();
                    newSponsor.PlayerSteamId = player.steamId.ToString();
                    newSponsor.PlayerFullName = player.GetFullName();
                    newSponsor.MentorId = 0;
                    newSponsor.MentorFullName = "";
                    newSponsor.MentorRewardClaimed = false;
                    newSponsor.LMenteePlayers = new List<int>();
                    newSponsor.MenteePlayers = ListConverter.WriteJson(newSponsor.LMenteePlayers);
                    newSponsor.LRewardRecovered = new List<int>();
                    newSponsor.RewardRecovered = ListConverter.WriteJson(newSponsor.LRewardRecovered);
                    newSponsor.ConnectionCount = 1;
                    newSponsor.LastConnection = DateUtils.GetNumericalDateOfTheDay();
                    newSponsor.CreatedAt = DateUtils.GetCurrentTime();

                    if (await newSponsor.Save())
                    {
                        player.Notify("Parrainage", "Vous êtes éligible au programme de parraiange", NotificationManager.Type.Success);
                        panel.Refresh();
                    } else
                    {
                        player.Notify("Parrainage", "Nous n'avons pas pu enregistrer votre inscription", NotificationManager.Type.Error);
                        panel.Refresh();
                    }
                });
            }
            panel.CloseButton();

            //Affichage
            panel.Display();
        }

        public void SponsorshipMentor(Player player, Sponsorship_Player currentPlayer)
        {
            //Déclaration
            Panel panel = Context.PanelHelper.Create("Parrainage - Parrain", UIPanel.PanelType.Text, player, () => SponsorshipMentor(player, currentPlayer));

            //Corps & Boutons
            if(currentPlayer.MentorId != default)
            {
                panel.TextLines.Add($"{mk.Color("Parrain:", mk.Colors.Info)} {currentPlayer.MentorFullName}");
                if(currentPlayer.ConnectionCount >= Sponsorship._sponsorshipConfig.ConnectionCountRequired && !currentPlayer.MentorRewardClaimed)
                {
                    panel.TextLines.Add($"Tu es éligible à la récompense de bienvenue !");
                    panel.TextLines.Add($"Cette récompense est unique, fais en bonne usage.");

                    panel.NextButton("Récompense", async () =>
                    {
                        currentPlayer.MentorRewardClaimed = true;
                        await currentPlayer.Save();

                        player.AddBankMoney(Sponsorship._sponsorshipConfig.MentorReward);
                        panel.Refresh();
                    });
                } else
                {
                    panel.TextLines.Add($"Vous devez avoir {mk.Color($"{Sponsorship._sponsorshipConfig.ConnectionCountRequired} jours d'ancienneté", mk.Colors.Orange)} pour être éligible à la récompense de bienvenue.");
                }
            } else
            {
                panel.TextLines.Add($"Chaque citoyen peut être parrainé");
                panel.TextLines.Add($"Une fois votre parrain selectionné, il n'est plus possible d'en changer");

                panel.NextButton("Suivant", () => SponsorshipMentorSearch(player, currentPlayer));
            }

            //Boutons
            panel.PreviousButton();
            panel.CloseButton();

            //Affichage
            panel.Display();
        }

        public async void SponsorshipMentorSearch(Player player, Sponsorship_Player currentPlayer)
        {
            //Query
            List<Sponsorship_Player> query = await Sponsorship_Player.QueryAll();
            query.RemoveAll(p => p.PlayerSteamId == player.steamId.ToString());
            //Déclaration
            Panel panel = Context.PanelHelper.Create("Parrainage - Chercher un parrain", UIPanel.PanelType.TabPrice, player, () => SponsorshipMentorSearch(player, currentPlayer));

            //Corps
            if (query != null & query.Count > 0)
            {
                foreach (Sponsorship_Player mentor in query)
                {
                    if(mentor.PlayerSteamId != player.steamId.ToString()) panel.AddTabLine($"{mentor.PlayerFullName}", _ => SponsorshipMentorSearchConfirm(player, currentPlayer, mentor));
                }
                panel.NextButton("Sélectionner", () => panel.SelectTab());
            }
            else panel.AddTabLine("Aucun parrain n'est disponible", _ => { });

            //Boutons
            panel.PreviousButton();
            panel.CloseButton();

            //Affichage
            panel.Display();
        }

        public void SponsorshipMentorSearchConfirm(Player player, Sponsorship_Player currentPlayer, Sponsorship_Player currentMentor)
        {
            //Déclaration
            Panel panel = Context.PanelHelper.Create("Parrainage - Confirmation du parrain", UIPanel.PanelType.Text, player, () => SponsorshipMentorSearchConfirm(player, currentPlayer, currentMentor));

            //Corps
            panel.TextLines.Add("Êtes-vous sûr de vouloir être parrainé par:");
            panel.TextLines.Add($"{mk.Color($"{currentMentor.PlayerFullName}", mk.Colors.Info)}");
            panel.TextLines.Add("Vous ne pourrez pas revenir sur cette décision.");


            //Boutons
            panel.CloseButtonWithAction("Confirmer", async () =>
            {
                currentPlayer.MentorId = currentMentor.Id;
                currentPlayer.MentorFullName = currentMentor.PlayerFullName;

                currentMentor.LMenteePlayers = ListConverter.ReadJson(currentMentor.MenteePlayers);
                if(!currentMentor.LMenteePlayers.Contains(currentPlayer.Id)) currentMentor.LMenteePlayers.Add(currentPlayer.Id);
                currentMentor.MenteePlayers = ListConverter.WriteJson(currentMentor.LMenteePlayers);

                if(await currentPlayer.Save() && await currentMentor.Save())
                {
                    player.Notify("Parrainage", "Parrainage enregistré", NotificationManager.Type.Success);
                    return true;
                }
                else
                {
                    player.Notify("Parrainage", "Nous n'avons pas pu enregistrer votre demande de parrainage", NotificationManager.Type.Error);
                    return false;
                }
            });
            panel.CloseButton();

            //Affichage
            panel.Display();
        }

        public async void SponsorshipMentee(Player player, Sponsorship_Player currentPlayer)
        {
            //Query
            List<Sponsorship_Player> query = await Sponsorship_Player.QueryAll();

            //Déclaration
            Panel panel = Context.PanelHelper.Create("Parrainage - Filleuls", UIPanel.PanelType.TabPrice, player, () => SponsorshipMentee(player, currentPlayer));

            //Corps
            if (currentPlayer.LMenteePlayers != null && currentPlayer.LMenteePlayers.Count > 0)
            {
                foreach (int menteeId in currentPlayer.LMenteePlayers)
                {
                    var currentMentee = query.Where(m => m.Id == menteeId).FirstOrDefault();
                    if(currentMentee != null) panel.AddTabLine($"{currentMentee.PlayerFullName}", $"{currentMentee.ConnectionCount} jour{(currentMentee.ConnectionCount>1?"s":"")} d'activité", IconUtils.Others.None.Id, _ => { });
                }
            }
            else panel.AddTabLine("Aucun filleul", _ => { });

            //Boutons
            panel.PreviousButton();
            panel.CloseButton();

            //Affichage
            panel.Display();
        }

        public async void SponsorshipReward(Player player, Sponsorship_Player currentPlayer)
        {
            List<Sponsorship_Reward> query = await Sponsorship_Reward.QueryAll();
            var rewards = query.OrderBy(p => p.MenteeRequired);

            List<Sponsorship_Player> allPlayers = await Sponsorship_Player.QueryAll();
            int count = 0;
            foreach (var menteeId in currentPlayer.LMenteePlayers)
            {
                var result = allPlayers.Where(m => m.Id == menteeId && m.ConnectionCount >= Sponsorship._sponsorshipConfig.ConnectionCountRequired).FirstOrDefault();
                if(result != null) count++;
            }

            Panel panel = Context.PanelHelper.Create($"Parrainage - Récompenses", UIPanel.PanelType.TabPrice, player, () => SponsorshipReward(player, currentPlayer));

            panel.AddTabLine($"{mk.Italic($"{mk.Size($"{mk.Color($"FILLEULS CONFIRMÉS {count}", mk.Colors.Info)}", 21)}")}", _ => { });

            foreach (var reward in rewards)
            {
                Item currentItem = ItemUtils.GetItemById(reward.ItemId);
                bool claimed = currentPlayer.LRewardRecovered.Contains(reward.Id);

                panel.AddTabLine($"{reward.ItemQuantity} x {currentItem.itemName}", $"{(claimed ? $"{mk.Color("récompense récupérée", mk.Colors.Grey)}" : $"requiert {reward.MenteeRequired} filleul{(reward.MenteeRequired>1?"s":"")}")}", ItemUtils.GetIconIdByItemId(reward.ItemId), async _ =>
                {
                    if (claimed)
                    {
                        player.Notify("Parrainage", "Vous avez déjà récupéré cette récompense", NotificationManager.Type.Info);
                        panel.Refresh();
                    }
                    else
                    {
                        if (count >= reward.MenteeRequired)
                        {
                            if (InventoryUtils.AddItem(player, reward.ItemId, reward.ItemQuantity))
                            {
                                currentPlayer.LRewardRecovered.Add(reward.Id);
                                currentPlayer.RewardRecovered = ListConverter.WriteJson(currentPlayer.LRewardRecovered);
                                if (await currentPlayer.Save())
                                {
                                    player.Notify("Parrainage", $"Vous venez d'obtenir {reward.ItemQuantity} {currentItem.itemName}", NotificationManager.Type.Success);
                                    panel.Refresh();
                                }
                                else
                                {
                                    InventoryUtils.RemoveFromInventory(player, reward.ItemId, reward.ItemQuantity);
                                    player.Notify("Parrainage", "Nous n'avons pas pu enregistrer votre récompense", NotificationManager.Type.Error);
                                    panel.Refresh();
                                }
                            }
                            else
                            {
                                player.Notify("Parrainage", "Vous n'avez pas suffisament d'espace dans votre inventaire", NotificationManager.Type.Warning);
                                panel.Refresh();
                            }
                        }
                        else
                        {
                            player.Notify("Parrainage", "Vous n'avez pas suffisament de prestige", NotificationManager.Type.Info);
                            panel.Refresh();
                        }
                    }
                });
            }

            panel.AddButton("Récupérer", _ => panel.SelectTab());
            panel.PreviousButton();
            panel.CloseButton();

            panel.Display();
        }
        #endregion

        /// <summary>
        /// Triggers the function to begin creating a new model.
        /// </summary>
        /// <param name="player">The player initiating the creation of the new model.</param>
        public void SetPatternData(Player player)
        {
            //Set the function to be called when a player clicks on the “create new model” button
            SetName(player);
        }
        /// <summary>
        /// Displays all properties of the pattern specified as parameter.
        /// The user can select one of the properties to make modifications.
        /// </summary>
        /// <param name="player">The player requesting to edit the pattern.</param>
        /// <param name="patternId">The ID of the pattern to be edited.</param>
        public async void EditPattern(Player player, int patternId)
        {
            Sponsorship_Point pattern = new Sponsorship_Point(false);
            pattern.Context = Context;
            await pattern.SetProperties(patternId);

            Panel panel = Context.PanelHelper.Create($"Modifier un {pattern.TypeName}", UIPanel.PanelType.Tab, player, () => EditPattern(player, patternId));


            panel.AddTabLine($"{mk.Color("Nom:", mk.Colors.Info)} {pattern.PatternName}", _ => {
                pattern.SetName(player, true);
            });

            panel.NextButton("Sélectionner", () => panel.SelectTab());
            panel.PreviousButton();
            panel.CloseButton();

            panel.Display();
        }

        /// <summary>
        /// Allows the player to set a name for the pattern, either during creation or modification.
        /// </summary>
        /// <param name="player">The player interacting with the panel.</param>
        /// <param name="inEdition">A flag indicating if the pattern is being edited.</param>
        public void SetName(Player player, bool isEditing = false)
        {
            Panel panel = Context.PanelHelper.Create($"{(!isEditing ? "Créer" : "Modifier")} un modèle de {TypeName}", UIPanel.PanelType.Input, player, () => SetName(player));

            panel.TextLines.Add("Donner un nom à votre boutique");
            panel.inputPlaceholder = "3 caractères minimum";

            if (!isEditing)
            {
                panel.NextButton("Suivant", async () =>
                {
                    if (panel.inputText.Length >= 3)
                    {
                        Sponsorship_Point newSponsorship = new Sponsorship_Point();
                        newSponsorship.TypeName = nameof(Sponsorship_Point);
                        newSponsorship.PatternName = panel.inputText;

                        if (await newSponsorship.Save())
                        {
                            player.Notify("Sponsorship", "Modifications enregistrées", NotificationManager.Type.Success);
                            ConfirmGeneratePoint(player, newSponsorship);
                        }
                        else
                        {
                            player.Notify("Sponsorship", "Nous n'avons pas pu enregistrer vos modifications", NotificationManager.Type.Error);
                            panel.Refresh();
                        }
                    }
                    else
                    {
                        player.Notify("Attention", "Vous devez donner un titre à votre boutique (3 caractères minimum)", Life.NotificationManager.Type.Warning);
                        panel.Refresh();
                    }
                });
            }
            else
            {
                panel.PreviousButtonWithAction("Confirmer", async () =>
                {
                    if (panel.inputText.Length >= 3)
                    {
                        PatternName = panel.inputText;
                        if (await Save()) return true;
                        else
                        {
                            player.Notify("Erreur", "échec lors de la sauvegarde de vos changements", Life.NotificationManager.Type.Error);
                            return false;
                        }
                    }
                    else
                    {
                        player.Notify("Attention", "Vous devez donner un titre à votre boutique (3 caractères minimum)", Life.NotificationManager.Type.Warning);
                        return false;
                    }
                });
            }
            panel.PreviousButton();
            panel.CloseButton();

            panel.Display();
        }

        #region REPLACE YOUR CLASS/TYPE AS PARAMETER
        /// <summary>
        /// Displays a panel allowing the player to select a pattern from a list of patterns.
        /// </summary>
        /// <param name="player">The player selecting the pattern.</param>
        /// <param name="patterns">The list of patterns to choose from.</param>
        /// <param name="configuring">A flag indicating if the player is configuring.</param>
        public void SelectPattern(Player player, List<Sponsorship_Point> patterns, bool configuring)
        {
            Panel panel = Context.PanelHelper.Create("Choisir un modèle", UIPanel.PanelType.Tab, player, () => SelectPattern(player, patterns, configuring));

            foreach (var pattern in patterns)
            {
                panel.AddTabLine($"{pattern.PatternName}", _ => { });
            }
            if (patterns.Count == 0) panel.AddTabLine($"Vous n'avez aucun modèle de {TypeName}", _ => { });

            if (!configuring && patterns.Count != 0)
            {
                panel.CloseButtonWithAction("Confirmer", async () =>
                {
                    if (await Context.PointHelper.CreateNPoint(player, patterns[panel.selectedTab])) return true;
                    else return false;
                });
            }
            else
            {
                panel.NextButton("Modifier", () => {
                    EditPattern(player, patterns[panel.selectedTab].Id);
                });
                panel.NextButton("Supprimer", () => {
                    ConfirmDeletePattern(player, patterns[panel.selectedTab]);
                });
            }

            panel.AddButton("Retour", ui =>
            {
                AAMenu.AAMenu.menu.AdminPointsSettingPanel(player);
            });
            panel.CloseButton();

            panel.Display();
        }

        /// <summary>
        /// Confirms the generation of a point with a previously saved pattern.
        /// </summary>
        /// <param name="player">The player confirming the point generation.</param>
        /// <param name="pattern">The pattern to generate the point from.</param>
        public void ConfirmGeneratePoint(Player player, Sponsorship_Point pattern)
        {
            Panel panel = Context.PanelHelper.Create($"Modèle \"{pattern.PatternName}\" enregistré !", UIPanel.PanelType.Text, player, () =>
            ConfirmGeneratePoint(player, pattern));

            panel.TextLines.Add($"Voulez-vous générer un point sur votre position avec ce modèle ?");

            panel.CloseButtonWithAction("Générer", async () =>
            {
                if (await Context.PointHelper.CreateNPoint(player, pattern)) return true;
                else return false;
            });
            panel.PreviousButton();
            panel.CloseButton();

            panel.Display();
        }
        #endregion

        #region DO NOT EDIT
        /// <summary>
        /// Base panel allowing the user to choose between creating a pattern from scratch
        /// or generating a point from an existing pattern.
        /// </summary>
        /// <param name="player">The player initiating the creation or generation.</param>
        public void CreateOrGenerate(Player player)
        {
            Panel panel = Context.PanelHelper.Create($"Créer ou générer un {TypeName}", UIPanel.PanelType.Text, player, () => CreateOrGenerate(player));

            panel.TextLines.Add(mk.Pos($"{mk.Align($"{mk.Color("Générer", mk.Colors.Info)} utiliser un modèle existant. Les données sont partagés entre les points utilisant un même modèle.", mk.Aligns.Left)}", 5));
            panel.TextLines.Add("");
            panel.TextLines.Add($"{mk.Align($"{mk.Color("Créer:", mk.Colors.Info)} définir un nouveau modèle de A à Z.", mk.Aligns.Left)}");

            panel.NextButton("Créer", () =>
            {
                SetPatternData(player);
            });
            panel.NextButton("Générer", async () =>
            {
                await GetPatternData(player, false);
            });
            panel.AddButton("Retour", ui =>
            {
                AAMenu.AAMenu.menu.AdminPointsPanel(player);
            });
            panel.CloseButton();

            panel.Display();
        }

        /// <summary>
        /// Retrieves all patterns before redirecting to a panel allowing the user various actions (CRUD).
        /// </summary>
        /// <param name="player">The player initiating the retrieval of pattern data.</param>
        /// <param name="configuring">A flag indicating if the user is configuring.</param>
        public async Task GetPatternData(Player player, bool configuring)
        {
            var patterns = await QueryAll();
            SelectPattern(player, patterns, configuring);
        }

        /// <summary>
        /// Confirms the deletion of the specified pattern.
        /// </summary>
        /// <param name="player">The player confirming the deletion.</param>
        /// <param name="patternData">The pattern data to be deleted.</param>
        public async void ConfirmDeletePattern(Player player, PatternData patternData)
        {
            var pattern = await Query(patternData.Id);

            Panel panel = Context.PanelHelper.Create($"Supprimer un modèle de {pattern.TypeName}", UIPanel.PanelType.Text, player, () =>
            ConfirmDeletePattern(player, patternData));

            panel.TextLines.Add($"Cette suppression entrainera également celle des points.");
            panel.TextLines.Add($"Êtes-vous sûr de vouloir supprimer le modèle \"{pattern.PatternName}\" ?");

            panel.PreviousButtonWithAction("Confirmer", async () =>
            {
                if (await Context.PointHelper.DeleteNPointsByPattern(player, pattern))
                {
                    if (await pattern.Delete())
                    {
                        return true;
                    }
                    else
                    {
                        player.Notify("Erreur", $"Nous n'avons pas pu supprimer le modèle \"{PatternName}\"", Life.NotificationManager.Type.Error, 6);
                        return false;
                    }
                }
                else
                {
                    player.Notify("Erreur", "Certains points n'ont pas pu être supprimés.", Life.NotificationManager.Type.Error, 6);
                    return false;
                }
            });
            panel.PreviousButton();
            panel.CloseButton();

            panel.Display();
        }

        /// <summary>
        /// Retrieves all NPoints before redirecting to a panel allowing various actions by the user.
        /// </summary>
        /// <param name="player">The player retrieving the NPoints.</param>
        public async Task GetNPoints(Player player)
        {
            var points = await NPoint.Query(e => e.TypeName == nameof(Sponsorship_Point));
            SelectNPoint(player, points);
        }

        /// <summary>
        /// Lists the points using this pattern.
        /// </summary>
        /// <param name="player">The player selecting the points.</param>
        /// <param name="points">The list of points to choose from.</param>
        public async void SelectNPoint(Player player, List<NPoint> points)
        {
            var patterns = await QueryAll();
            Panel panel = Context.PanelHelper.Create($"Points de type {nameof(Sponsorship_Point)}", UIPanel.PanelType.Tab, player, () => SelectNPoint(player, points));

            if (points.Count > 0)
            {
                foreach (var point in points)
                {
                    var currentPattern = patterns.FirstOrDefault(p => p.Id == point.PatternId);
                    panel.AddTabLine($"point n° {point.Id}: {(currentPattern != default ? currentPattern.PatternName : "???")}", _ => { });
                }

                panel.NextButton("Voir", () =>
                {
                    DisplayNPoint(player, points[panel.selectedTab]);
                });
                panel.NextButton("Supprimer", async () =>
                {
                    await Context.PointHelper.DeleteNPoint(points[panel.selectedTab]);
                    await GetNPoints(player);
                });
            }
            else
            {
                panel.AddTabLine($"Aucun point de ce type", _ => { });
            }
            panel.AddButton("Retour", ui =>
            {
                AAMenu.AAMenu.menu.AdminPointsSettingPanel(player);
            });
            panel.CloseButton();

            panel.Display();
        }

        /// <summary>
        /// Displays the information of a point and allows the user to modify it.
        /// </summary>
        /// <param name="player">The player viewing the point information.</param>
        /// <param name="point">The point to display information for.</param>
        public async void DisplayNPoint(Player player, NPoint point)
        {
            var pattern = await Query(p => p.Id == point.PatternId);
            Panel panel = Context.PanelHelper.Create($"Point n° {point.Id}", UIPanel.PanelType.Tab, player, () => DisplayNPoint(player, point));

            panel.AddTabLine($"Type: {point.TypeName}", _ => { });
            panel.AddTabLine($"Modèle: {(pattern[0] != null ? pattern[0].PatternName : "???")}", _ => { });
            panel.AddTabLine($"", _ => { });
            panel.AddTabLine($"Position: {point.Position}", _ => { });


            panel.AddButton("TP", ui =>
            {
                Context.PointHelper.PlayerSetPositionToNPoint(player, point);
            });
            panel.AddButton("Définir pos.", async ui =>
            {
                await Context.PointHelper.SetNPointPosition(player, point);
                panel.Refresh();
            });
            panel.PreviousButton();
            panel.CloseButton();

            panel.Display();
        }
        #endregion
    }
}
