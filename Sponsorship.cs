﻿using Life;
using ModKit.Helper;
using ModKit.Interfaces;
using Sponsorship.Entities;

namespace Sponsorship
{
    public class Sponsorship : ModKit.ModKit
    {
       
        public Sponsorship(IGameAPI api) : base(api)
        {
            PluginInformations = new PluginInformations(AssemblyHelper.GetName(), "1.0.0", "Aarnow");
        }

        public override void OnPluginInit()
        {
            base.OnPluginInit();

            Orm.RegisterTable<Sponsorship_Player>();

            ModKit.Internal.Logger.LogSuccess($"{PluginInformations.SourceName} v{PluginInformations.Version}", "initialisé");
        }
    }
}
