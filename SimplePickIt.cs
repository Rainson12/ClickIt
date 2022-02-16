﻿using ExileCore;
using ExileCore.PoEMemory.Components;
using ExileCore.PoEMemory.Elements;
using ExileCore.PoEMemory.MemoryObjects;
using ExileCore.Shared.Cache;
using ExileCore.Shared.Enums;
using ExileCore.Shared.Helpers;
using SharpDX;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Forms;

namespace SimplePickIt
{
    public class SimplePickIt : BaseSettingsPlugin<SimplePickItSettings>
    {
        private Stopwatch Timer { get; } = new Stopwatch();
        private Random Random { get; } = new Random();
        public static SimplePickIt Controller { get; set; }
        public int[,] inventorySlots { get; set; } = new int[0, 0];
        public ServerInventory InventoryItems { get; set; }
        private TimeCache<List<LabelOnGround>> CachedLabels { get; set; }
        private RectangleF Gamewindow;
        public override bool Initialise()
        {
            Controller = this;
            Gamewindow = GameController.Window.GetWindowRectangle();
            CachedLabels = new TimeCache<List<LabelOnGround>>(UpdateLabelComponent, Settings.CacheIntervall);
            Timer.Start();
            return true;
        }

        public override Job Tick()
        {
            InventoryItems = GameController.Game.IngameState.ServerData.PlayerInventories[0].Inventory;
            inventorySlots = Misc.GetContainer2DArray(InventoryItems);
            if (!Input.GetKeyState(Settings.ClickLabelKey.Value)) return null;
            if (GameController.Game.IngameState.IngameUi.ItemsOnGroundLabelsVisible.Count < 1) return null;
            if (Timer.ElapsedMilliseconds < Settings.WaitTimeInMs.Value - 10 + Random.Next(0, 20)) return null;
            if (GameController.Area.CurrentArea.IsHideout || GameController.Area.CurrentArea.IsTown) return null;
           
            Timer.Restart();
            ClickLabel();
            //return new Job("SimplePickIt", PickItem);
            return null;
        }

        private List<LabelOnGround> UpdateLabelComponent() =>
            GameController.Game.IngameState.IngameUi.ItemsOnGroundLabelsVisible
            .Where(x =>
                x.ItemOnGround?.Path != null &&
                x.Label.GetClientRect().Center.PointInRectangle(new RectangleF(0, 0, Gamewindow.Width, Gamewindow.Height)) &&
                x.CanPickUp &&
                (x.ItemOnGround.Type == EntityType.WorldItem ||
                x.ItemOnGround.Type == EntityType.Chest && !x.ItemOnGround.GetComponent<Chest>().OpenOnDamage ||
                x.ItemOnGround.Type == EntityType.AreaTransition))
            .OrderBy(x => x.ItemOnGround.DistancePlayer)
            .ToList();

        private void ClickLabel()
        {
            if (Settings.DebugMode) LogMessage("Trying to Click Label");
            Gamewindow = GameController.Window.GetWindowRectangle();
            var nextLabel = GetLabel();
            if (nextLabel == null)
            {
                if (Settings.DebugMode) LogMessage("nextLabel in ClickLabel() is null");
                return;
            }

            var centerOfLabel = nextLabel?.Label?.GetClientRect().Center 
                + Gamewindow.TopLeft
                + new Vector2(Random.Next(0, 2), Random.Next(0, 2));
            
            if (!centerOfLabel.HasValue)
            {
                if (Settings.DebugMode) LogMessage("centerOfLabel has no Value");
                return;
            }
            Input.SetCursorPos(centerOfLabel.Value);
            Input.Click(MouseButtons.Left);
        }
        private LabelOnGround GetLabel()
        {


            var label = CachedLabels.Value.Find(x => x.ItemOnGround.DistancePlayer <= Settings.ClickDistance && 
                (Settings.ClickItems.Value && 
                x.ItemOnGround.Type == EntityType.WorldItem && 
                    (!x.ItemOnGround.GetComponent<WorldItem>().ItemEntity.Path.StartsWith("Metadata/Items/Archnemesis/ArchnemesisMod") && Misc.CanFitInventory(new CustomItem(x, GameController.Files)) || 
                    (x.ItemOnGround.GetComponent<WorldItem>().ItemEntity.Path.StartsWith("Metadata/Items/Archnemesis/ArchnemesisMod") && !GameController.IngameState.IngameUi.ArchnemesisInventoryPanel.InventoryFull)) && 
                (!Settings.IgnoreUniques || x.ItemOnGround.GetComponent<WorldItem>()?.ItemEntity.GetComponent<Mods>()?.ItemRarity != ItemRarity.Unique || 
                x.ItemOnGround.GetComponent<WorldItem>().ItemEntity.Path.StartsWith("Metadata/Items/Metamorphosis/Metamorphosis")) ||
                Settings.ClickChests.Value && x.ItemOnGround.Type == EntityType.Chest ||
                Settings.ClickAreaTransitions.Value && x.ItemOnGround.Type == EntityType.AreaTransition));
            return label;
        }
    }
}
