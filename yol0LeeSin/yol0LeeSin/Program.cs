﻿using System;
using System.Collections.Generic;
using System.Linq;
using LeagueSharp;
using LeagueSharp.Common;
using SharpDX;
using Color = System.Drawing.Color;
// ReSharper disable InconsistentNaming

/* TODO:
 * Ult multi-knockup
 * Insec move to mouse
 * x 
 * Add smite usage/jungle steal
 *  -> Smite champions
 *  -> Q->Smite->Q2 jungle steal
 *  -> Smite jungle creeps
 * Add W to gapclose in combo mode if Q is down
 * Test harass mode
 * Jungle escape spots with Q
 * Add item damage into damage calculations?
 * 
 * Auto W2/E2 when they are about to expire - AutoSpells()
 * Add ward placement in SpellDodger?
 */

/* DONE:
 * Improve non-kill combo to use passive properly
 * Obey combo settings :P
 * Fix Wardjump/Flash insec mode to use flash
 * Add wardjump range check for mouse so it doesn't hop to the ward 20 units away or the wrong direction
 * (alpha) Test/Improve GetInsecQTarget()
 */

namespace yol0LeeSin
{
    internal class Program
    {
        public static Spell _Q = new Spell(SpellSlot.Q, 1000);
        public static Spell _Q2 = new Spell(SpellSlot.Q, 1300);
        public static Spell _W = new Spell(SpellSlot.W, 700);
        public static Spell _E = new Spell(SpellSlot.E, 350);
        public static Spell _E2 = new Spell(SpellSlot.E, 500);
        public static Spell _R = new Spell(SpellSlot.R, 375);
        public static Spell _F = new Spell(SpellSlot.Unknown, 425);
        public static Spell _I = new Spell(SpellSlot.Unknown, 600);
        public static Menu _menu;
        private static Orbwalking.Orbwalker _orbwalker;
        private static GameObject _ward;
        private static Obj_AI_Base _insecQTarget;
        private static int pCount;
        private static int wTimer;
        private static int eTimer;
        private static int lastWardCast;
        private static Obj_AI_Hero _target;
        private static bool inKillCombo;
        private static List<Spell> _killCombo;
        private static int _comboStep;
        private static Spell _lastSpell;
        private static Spell _nextSpell;
        private static int _lastSpellCast;
        private static readonly Items.Item _Tiamat = new Items.Item(3077, 185);
        private static readonly Items.Item _Hydra = new Items.Item(3074, 185);
        private static readonly Items.Item _Ghostblade = new Items.Item(3142);
        private static readonly Items.Item _Bilgewater = new Items.Item(3144, 450);
        private static readonly Items.Item _Botrk = new Items.Item(3153, 450);
        private static readonly Items.Item _Hextech = new Items.Item(3146, 700);
        private static readonly Items.Item _Randuins = new Items.Item(3143, 500);

        private static Obj_AI_Hero Player
        {
            get { return ObjectManager.Player; }
        }

        private static bool IsQOne
        {
            get { return _Q.Instance.Name == "BlindMonkQOne"; }
        }

        private static bool IsQTwo
        {
            get { return _Q.Instance.Name == "blindmonkqtwo"; }
        }

        private static bool IsWOne
        {
            get { return _W.Instance.Name == "BlindMonkWOne"; }
        }

        private static bool IsEOne
        {
            get { return _E.Instance.Name == "BlindMonkEOne"; }
        }

        private static bool IsETwo
        {
            get { return _E.Instance.Name == "blindmonketwo"; }
        }

        private static bool UseQ
        {
            get { return _menu.SubMenu("Combo").Item("useQ").GetValue<bool>(); }
        }

        private static bool UseI
        {
            get { return _menu.SubMenu("Combo").Item("useI").GetValue<bool>(); }
        }

        private static bool UseQ2
        {
            get { return _menu.SubMenu("Combo").Item("useQ2").GetValue<bool>(); }
        }

        private static bool UseE
        {
            get { return _menu.SubMenu("Combo").Item("useE").GetValue<bool>(); }
        }

        private static bool UseE2
        {
            get { return _menu.SubMenu("Combo").Item("useE2").GetValue<bool>(); }
        }

        private static bool UseR
        {
            get { return _menu.SubMenu("Combo").Item("useR").GetValue<bool>(); }
        }

        private static bool HUseQ
        {
            get { return _menu.SubMenu("Harass").Item("useQ").GetValue<bool>(); }
        }

        private static bool HUseQ2
        {
            get { return _menu.SubMenu("Harass").Item("useQ2").GetValue<bool>(); }
        }

        private static bool HUseE
        {
            get { return _menu.SubMenu("Harass").Item("useE").GetValue<bool>(); }
        }

        private static bool HUseE2
        {
            get { return _menu.SubMenu("Harass").Item("useE2").GetValue<bool>(); }
        }

        private static void Main(string[] args)
        {
            CustomEvents.Game.OnGameLoad += OnGameLoad;
        }

        private static void OnGameLoad(EventArgs args)
        {
            _menu = new Menu("yol0 LeeSin", "yol0LeeSin", true);
            _menu.AddSubMenu(new Menu("Target Selector", "Target Selector"));
            _menu.AddSubMenu(new Menu("Orbwalker", "Orbwalker"));
            _menu.AddSubMenu(new Menu("Keys", "Keys"));
            _menu.AddSubMenu(new Menu("Combo", "Combo"));
            _menu.AddSubMenu(new Menu("Harass", "Harass"));
            _menu.AddSubMenu(new Menu("Insec", "Insec"));
            _menu.AddSubMenu(new Menu("Dodge", "Dodge"));
            _menu.AddSubMenu(new Menu("Drawing", "Draw"));
            
            TargetSelector.AddToMenu(_menu.SubMenu("Target Selector"));
            _orbwalker = new Orbwalking.Orbwalker(_menu.SubMenu("Orbwalker"));

            _menu.SubMenu("Keys")
                .AddItem(new MenuItem("Insec", "Insec").SetValue(new KeyBind("X".ToArray()[0], KeyBindType.Press)));
            _menu.SubMenu("Keys")
                .AddItem(new MenuItem("Escape", "Escape").SetValue(new KeyBind("A".ToArray()[0], KeyBindType.Press)));
            _menu.SubMenu("Keys")
                .AddItem(new MenuItem("Wardjump", "Ward Jump").SetValue(new KeyBind("Z".ToArray()[0], KeyBindType.Press)));

            _menu.SubMenu("Combo").AddItem(new MenuItem("useQ", "Use Q").SetValue(true));
            _menu.SubMenu("Combo").AddItem(new MenuItem("useQ2", "Use Q2").SetValue(true));
            _menu.SubMenu("Combo").AddItem(new MenuItem("useE", "Use E").SetValue(true));
            _menu.SubMenu("Combo").AddItem(new MenuItem("useE2", "Use E2").SetValue(true));
            _menu.SubMenu("Combo").AddItem(new MenuItem("useR", "Use R").SetValue(true));
            _menu.SubMenu("Combo").AddItem(new MenuItem("useI", "Use Ignite").SetValue(true));
            _menu.SubMenu("Combo").AddItem(new MenuItem("useItems", "Use Items").SetValue(true));
            _menu.SubMenu("Combo")
                .AddItem(
                    new MenuItem("qHitchance", "Q Hitchance").SetValue(
                        new StringList(new[] {"Low", "Medium", "High", "VeryHigh"}, 1)));

            /*_menu.SubMenu("Harass").AddItem(new MenuItem("useQ", "Use Q").SetValue(true));
            _menu.SubMenu("Harass").AddItem(new MenuItem("useQ2", "Use Q2").SetValue(true));
            _menu.SubMenu("Harass").AddItem(new MenuItem("useW", "W Away").SetValue(true));
            _menu.SubMenu("Harass").AddItem(new MenuItem("useE", "Use E").SetValue(true));
            _menu.SubMenu("Harass").AddItem(new MenuItem("useE2", "Use E2").SetValue(true));
            _menu.SubMenu("Harass").AddItem(new MenuItem("useItems", "Use Items").SetValue(true));
            _menu.SubMenu("Harass")
                .AddItem(
                    new MenuItem("qHitchance", "Q Hitchance").SetValue(
                        new StringList(new[] {"Low", "Medium", "High", "VeryHigh"}, 1)));*/

            _menu.SubMenu("Insec")
                .AddItem(
                    new MenuItem("method", "Insec Method").SetValue(
                        new StringList(new[] {"Wardjump only", "Flash Only", "Wardjump + Flash"}, 2)));
            _menu.SubMenu("Insec")
                .AddItem(
                    new MenuItem("mode", "Insec Mode").SetValue(
                        new StringList(new[] {"To Ally", "To Mouse", "To Turret"})));
            _menu.SubMenu("Insec").AddItem(new MenuItem("qCreep", "Q to enemy near target (experimental)").SetValue(false));

            _menu.SubMenu("Draw").AddItem(new MenuItem("drawQ", "Draw Q Range").SetValue(new Circle(true, Color.Green)));
            _menu.SubMenu("Draw").AddItem(new MenuItem("drawW", "Draw W Range").SetValue(new Circle(true, Color.Green)));
            _menu.SubMenu("Draw").AddItem(new MenuItem("drawE", "Draw E Range").SetValue(new Circle(true, Color.Green)));
            _menu.SubMenu("Draw").AddItem(new MenuItem("drawR", "Draw R Range").SetValue(new Circle(true, Color.Green)));

            _menu.SubMenu("Draw").AddItem(new MenuItem("drawInsec", "Draw Insec").SetValue(true));
            _menu.SubMenu("Draw").AddItem(new MenuItem("drawDamage", "Draw Damage on Healthbar").SetValue(true));
            _menu.SubMenu("Draw").AddItem(new MenuItem("drawCombo", "Draw Kill Combo").SetValue(true));
            _menu.SubMenu("Draw").AddItem(new MenuItem("drawTarget", "Draw Target").SetValue(true));

            _menu.AddToMainMenu();

            SpellDodger.Initialize(_menu.SubMenu("Dodge"));

            _I.Slot = Player.GetSpellSlot("summonerdot");
            _F.Slot = Player.GetSpellSlot("summonerflash");

            _Q.SetSkillshot(0.25f, 60f, 1750f, true, SkillshotType.SkillshotLine);

            Utility.HpBarDamageIndicator.DamageToUnit = enemy => (float) GetDamage(enemy);
            GameObject.OnCreate += OnCreateObject;
            Obj_AI_Base.OnProcessSpellCast += OnProcessSpellCast;
            Obj_AI_Base.OnProcessSpellCast += KillCombo_OnProcessSpellCast;
            Game.OnUpdate += OnUpdate;
            Drawing.OnDraw += OnDraw;
        }

        #region Damage Calculations

        private static double GetDamage(Obj_AI_Hero target)
        {
            var qDmg = UseQ && _Q.IsReady() ? Player.GetSpellDamage(target, SpellSlot.Q) : 0.0;
            if (UseQ && UseQ2 && _Q.IsReady() && _Q.Instance.Name == "BlindMonkQOne")
            {
                qDmg += Player.GetSpellDamage(target, SpellSlot.Q, 1);
            }
            var eDmg = UseE && _E.IsReady() ? Player.GetSpellDamage(target, SpellSlot.E) : 0.0;
            var rDmg = UseR && _R.IsReady() ? Player.GetSpellDamage(target, SpellSlot.R) : 0.0;
            var aDmg = Player.GetAutoAttackDamage(target) * 0.87;
            var iDmg = 0.0;

            if (UseI && _I.Slot != SpellSlot.Unknown && _I.IsReady())
                iDmg = Player.GetSummonerSpellDamage(target, Damage.SummonerSpell.Ignite);

            return qDmg + eDmg + rDmg + aDmg*2 + iDmg;
        }

        #endregion

        #region Events

        private static void OnDraw(EventArgs args)
        {
            if (_menu.SubMenu("Draw").Item("drawQ").GetValue<Circle>().Active)
            {
                Render.Circle.DrawCircle(Player.Position, _Q.Range,
                    _menu.SubMenu("Draw").Item("drawQ").GetValue<Circle>().Color);
            }

            if (_menu.SubMenu("Draw").Item("drawW").GetValue<Circle>().Active)
            {
                Render.Circle.DrawCircle(Player.Position, _W.Range,
                    _menu.SubMenu("Draw").Item("drawW").GetValue<Circle>().Color);
            }

            if (_menu.SubMenu("Draw").Item("drawE").GetValue<Circle>().Active)
            {
                Render.Circle.DrawCircle(Player.Position, _E.Range,
                    _menu.SubMenu("Draw").Item("drawE").GetValue<Circle>().Color);
            }

            if (_menu.SubMenu("Draw").Item("drawR").GetValue<Circle>().Active)
            {
                Render.Circle.DrawCircle(Player.Position, _R.Range,
                    _menu.SubMenu("Draw").Item("drawR").GetValue<Circle>().Color);
            }

            if (_menu.SubMenu("Draw").Item("drawTarget").GetValue<bool>() && _target != null && !_target.IsDead)
            {
                Render.Circle.DrawCircle(_target.Position, _target.BoundingRadius, Color.Red);
                Render.Circle.DrawCircle(_target.Position, _target.BoundingRadius + 10, Color.Red);
                Render.Circle.DrawCircle(_target.Position, _target.BoundingRadius + 25, Color.Red);
            }

            if (_menu.SubMenu("Draw").Item("drawInsec").GetValue<bool>() && _target != null && !_target.IsDead &&
                Hud.SelectedUnit != null && _target.NetworkId == Hud.SelectedUnit.NetworkId && _R.IsReady())
            {
                var insecPos = GetInsecPosition(_target);
                Render.Circle.DrawCircle(insecPos.To3D(), 40f, Color.Green);
                var dirPos = (_target.Position.To2D() - insecPos).Normalized();
                var endPos = _target.Position.To2D() + (dirPos*1200);

                var wts1 = Drawing.WorldToScreen(insecPos.To3D());
                var wts2 = Drawing.WorldToScreen(endPos.To3D());

                Drawing.DrawLine(wts1, wts2, 2, Color.Green);
            }

            if (_menu.SubMenu("Draw").Item("drawCombo").GetValue<bool>())
            {
                foreach (var enemy in HeroManager.Enemies.Where(hero => hero.IsVisible && !hero.IsDead))
                {
                    var pos = enemy.HPBarPosition;
                    pos.Y += 35;
                    pos.X += 5;
                    var combo = ComboGenerator.GetKillCombo(enemy);
                    Drawing.DrawText(pos.X, pos.Y, combo != null ? Color.Green : Color.Red,
                        combo != null ? ComboGenerator.ComboString(combo) : "Not Killable");
                }
            }
        }

        private static void OnCreateObject(GameObject sender, EventArgs args)
        {
            if (Player.Distance(sender.Position) <= 700 && sender.IsAlly &&
                (sender.Name == "VisionWard" || sender.Name == "SightWard"))
            {
                _ward = sender;
            }
        }

        private static void Buff()
        {
            var p = false;
            foreach (var buff in Player.Buffs.Where(buff => buff.DisplayName == "BlindMonkFury"))
            {
                p = true;
                pCount = buff.Count;
            }

            if (!p)
                pCount = 0;
        }

        private static void OnUpdate(EventArgs args)
        {
            Utility.HpBarDamageIndicator.Enabled = _menu.SubMenu("Draw").Item("drawDamage").GetValue<bool>();
            _target = TargetSelector.GetTarget(2000, TargetSelector.DamageType.Physical);
            Buff();
            if (_menu.SubMenu("Keys").Item("Escape").GetValue<KeyBind>().Active)
            {
                Escape();
            }

            if (_menu.SubMenu("Keys").Item("Wardjump").GetValue<KeyBind>().Active)
            {
                Wardjump();
            }

            if (_menu.SubMenu("Keys").Item("Insec").GetValue<KeyBind>().Active)
            {
                if (Hud.SelectedUnit != null && Hud.SelectedUnit is Obj_AI_Hero)
                    Insec((Obj_AI_Hero) Hud.SelectedUnit);
            }

            if (_orbwalker.ActiveMode == Orbwalking.OrbwalkingMode.Combo && _target != null)
            {
                Combo(_target);
            }
            /*else if (_orbwalker.ActiveMode == Orbwalking.OrbwalkingMode.Mixed && _target != null)
            {
                Harass(_target);
            }*/
        }

        private static void OnProcessSpellCast(Obj_AI_Base sender, GameObjectProcessSpellCastEventArgs args)
        {
            if (sender.IsMe)
            {
                if (args.SData.Name == "BlindMonkWOne")
                    wTimer = Utils.GameTimeTickCount + 2700;
                else if (args.SData.Name == "BlindMonkEOne")
                    eTimer = Utils.GameTimeTickCount + 2700;
                else if (args.SData.Name == "BlindMonkRKick")
                    Orbwalking.ResetAutoAttackTimer();

                else if (args.SData.Name.Contains("ward") || args.SData.Name.Contains("Trinket"))
                    lastWardCast = Utils.GameTimeTickCount;
            }
        }

        #endregion

        #region Combo

        private static void UseItems(Obj_AI_Hero target)
        {
            if (_Tiamat.IsReady() && target.IsValidTarget(_Tiamat.Range))
            {
                _Tiamat.Cast();
            }
            if (_Hydra.IsReady() && target.IsValidTarget(_Hydra.Range))
            {
                _Hydra.Cast();
            }
            if (_Bilgewater.IsReady() && target.IsValidTarget(_Bilgewater.Range))
            {
                _Bilgewater.Cast(target);
            }
            if (_Botrk.IsReady() && target.IsValidTarget(_Botrk.Range))
            {
                _Botrk.Cast(target);
            }
            if (_Hextech.IsReady() && target.IsValidTarget(_Hextech.Range))
            {
                _Hextech.Cast(target);
            }
            if (_Ghostblade.IsReady() && target.IsValidTarget(600))
            {
                _Ghostblade.Cast();
            }
            if (_Randuins.IsReady() && target.IsValidTarget(_Randuins.Range))
            {
                _Randuins.Cast();
            }
        }

        private static void AutoSkills() // TODO
        {
            if (_menu.SubMenu("Combo").Item("useW2").GetValue<bool>() && wTimer < Utils.GameTimeTickCount)
            {
                _W.Cast();
            }
            if (_menu.SubMenu("Combo").Item("useE2").GetValue<bool>() && eTimer < Utils.GameTimeTickCount)
            {
                _E.Cast();
            }
        }

        private static void Combo(Obj_AI_Hero target)
        {
            if (_menu.SubMenu("Combo").Item("useItems").GetValue<bool>())
                UseItems(target);

            if (!KillCombo(target))
            {
                _killCombo = null;
                _nextSpell = null;
                _lastSpell = null;
                _comboStep = 0;
                inKillCombo = false;
                if (_lastSpellCast + 300 <= Utils.GameTimeTickCount)
                {
                    if (UseQ && _Q.IsReady() && IsQOne && target.IsValidTarget(_Q.Range))
                    {
                        _lastSpellCast = Utils.GameTimeTickCount;
                        CastQCombo(target);
                    }

                    if (UseE && _E.IsReady() && IsEOne && target.IsValidTarget(_E.Range) && pCount < 1)
                    {
                        _lastSpellCast = Utils.GameTimeTickCount;
                        _E.Cast();
                    }

                    if (target.HasBuff("BlindMonkQOne") && UseQ2 && IsQTwo)
                    {
                        _lastSpellCast = Utils.GameTimeTickCount;
                        _Q2.CastOnUnit(target);
                    }

                    if (UseE2 && _E2.IsReady() && IsETwo && target.IsValidTarget(_E2.Range) && Player.Mana >= 50 && pCount == 0)
                    {
                        _lastSpellCast = Utils.GameTimeTickCount;
                        _E2.Cast();
                    }
                }
            }
        }

        private static void CastRMultiple(Obj_AI_Hero target, int min)
        {
            //next update :)
        }

        private static bool KillCombo(Obj_AI_Hero target)
        {
            var tmpCombo = ComboGenerator.GetKillCombo(target);
            if (tmpCombo != null && _killCombo == null)
            {
                _killCombo = tmpCombo;
                _comboStep = 0;
                _lastSpell = null;
                _nextSpell = _killCombo[_comboStep];
                inKillCombo = true;
                ComboGenerator.PrintCombo(_killCombo);
                return true;
            }
            if (tmpCombo == null && inKillCombo && _killCombo != null && _comboStep == _killCombo.Count)
            {
                _killCombo = null;
                _comboStep = 0;
                _lastSpell = null;
                _nextSpell = null;
                inKillCombo = false;
                return false;
            }

            if (_killCombo == null)
            {
                return false;
            }

            if (_lastSpellCast + 250 > Utils.GameTimeTickCount) 
                return true;


            if (!_nextSpell.IsReady(1) && _nextSpell != _Q2)
            {
                return false;
            }

            if (Player.Distance(target.Position) > _nextSpell.Range)
            {
                return false;
            }

            if (target.IsDead)
                return false;

            if (_nextSpell == _Q)
            {
                _lastSpellCast = Utils.GameTimeTickCount;
                CastQCombo(target);
            }
            else if (_nextSpell == _Q2)
            {
                _lastSpellCast = Utils.GameTimeTickCount;
                _Q2.CastOnUnit(target);
            }
            else if (_nextSpell == _E)
            {
                _lastSpellCast = Utils.GameTimeTickCount;
                _E.Cast();
            }
            else if (_nextSpell == _I)
            {
                _lastSpellCast = Utils.GameTimeTickCount;
                _I.CastOnUnit(target);
            }
            else if (_nextSpell == _R)
            {
                _lastSpellCast = Utils.GameTimeTickCount;
                _R.CastOnUnit(target);
            }

            return true;
        }

        private static void KillCombo_OnProcessSpellCast(Obj_AI_Base sender, GameObjectProcessSpellCastEventArgs args)
        {
            if (!sender.IsMe)
                return;

            switch (args.SData.Name)
            {
                case "BlindMonkQOne":
                    _lastSpell = _Q;
                    _lastSpellCast = Utils.GameTimeTickCount;
                    break;
                case "blindmonkqtwo":
                    _lastSpell = _Q2;
                    _lastSpellCast = Utils.GameTimeTickCount;
                    break;
                case "BlindMonkEOne":
                    _lastSpell = _E;
                    _lastSpellCast = Utils.GameTimeTickCount;
                    break;
                case "BlindMonkRKick":
                    _lastSpell = _R;
                    _lastSpellCast = Utils.GameTimeTickCount;
                    break;
                case "summonerdot":
                    _lastSpell = _I;
                    _lastSpellCast = Utils.GameTimeTickCount;
                    break;
                default:
                    return;
            }

            if (!inKillCombo)
                return;

            if (_killCombo != null && _killCombo.Count - 1 > _comboStep && _killCombo[_comboStep] == _lastSpell)
            {
                _nextSpell = _killCombo[++_comboStep];
            }
            else
            {
                inKillCombo = false;
                _killCombo = null;
                _comboStep = 0;
                _nextSpell = null;
            }
        }

        #endregion

        #region Harass
        /* Not quite ready for harass yet :)
        private static Obj_AI_Base GetHarassObject(Obj_AI_Hero target)
        {
            var allies =
                HeroManager.Allies.Where(hero => hero.Distance(target.Position) > 350)
                    .OrderBy(hero => hero.Distance(target.Position))
                    .ToList();

            var minions =
                MinionManager.GetMinions(Player.Position, 700, MinionTypes.All, MinionTeam.Ally)
                    .OrderBy(minion => minion.Distance(target.Position))
                    .ToList();

            var wards =
                ObjectManager.Get<Obj_AI_Minion>()
                    .Where(
                        obj =>
                            (obj.Name.Contains("Ward") || obj.Name.Contains("ward") || obj.Name.Contains("Trinket")) &&
                            obj.IsAlly && Player.Distance(obj.Position) <= 700)
                    .OrderBy(obj => obj.Distance(target.Position))
                    .ToList();

            foreach (var ally in allies)
            {
                if (!ally.IsMe)
                    return ally;
            }

            if (_ward != null && _ward.IsValid && !_ward.IsDead && Player.Distance(_ward.Position) <= 700)
            {
                return _ward as Obj_AI_Base;
            }

            foreach (var ward in wards)
            {
                return ward;
            }

            return minions.FirstOrDefault();
        }

        private static void Harass(Obj_AI_Hero target)
        {
            if (_lastSpellCast + 300 <= Utils.GameTimeTickCount)
            {
                if (HUseQ && _Q.IsReady() && IsQOne && target.IsValidTarget(_Q.Range))
                {
                    _lastSpellCast = Utils.GameTimeTickCount;
                    CastQHarass(target);
                }
                if (HUseE && _E.IsReady() && IsEOne && target.IsValidTarget(_E.Range))
                {
                    _lastSpellCast = Utils.GameTimeTickCount;
                    _E.Cast();
                }

                if (target.HasBuff("BlindMonkQOne") && HUseQ2 && IsQTwo)
                {
                    _Q2.CastOnUnit(target);
                }
                if (HUseE2 && _E2.IsReady() && IsETwo && target.IsValidTarget(_E2.Range) && Player.Mana >= 50)
                {
                    _E2.Cast();
                }

                if (!(_Q.IsReady() && HUseQ) && !(_E.IsReady() && HUseE))
                {
                    var obj = GetHarassObject(target);
                    if (obj != null && _W.IsReady() && IsWOne)
                    {
                        _W.CastOnUnit(obj);
                    }
                }
            }
        }*/

        #endregion

        #region Escape

        private static InventorySlot GetWardSlot()
        {
            var wardNames = new[]
            {
                "Warding Totem (Trinket)", "Greater Totem (Trinket)", "Greater Stealth Totem (Trinket)", "Ruby Sightstone",
                "Sightstone", "Stealth Ward"
            };
            return wardNames.Select(name => Player.InventoryItems.FirstOrDefault(slot => slot.DisplayName == name)).FirstOrDefault(id => id.IsValidSlot() && Player.Spellbook.CanUseSpell(id.SpellSlot) == SpellState.Ready);
        }

        private static bool CanCastWard()
        {
            return _W.Instance.Name == "BlindMonkWOne" && Utils.GameTimeTickCount - 2000 > lastWardCast;
        }

        private static Obj_AI_Base GetEscapeObject(Vector3 pos, int range = 700)
        {
            var allies =
                HeroManager.Allies.Where(hero => hero.Distance(pos) <= range)
                    .OrderBy(hero => hero.Distance(pos))
                    .ToList();
            var minions =
                MinionManager.GetMinions(pos, range, MinionTypes.All, MinionTeam.Ally)
                    .OrderBy(minion => minion.Distance(pos))
                    .ToList();
            var wards =
                ObjectManager.Get<Obj_AI_Minion>()
                    .Where(
                        obj =>
                            (obj.Name.Contains("Ward") || obj.Name.Contains("ward") || obj.Name.Contains("Trinket")) &&
                            obj.IsAlly && pos.Distance(obj.Position) <= range)
                    .OrderBy(obj => obj.Distance(pos))
                    .ToList();

            foreach (var ally in allies.Where(ally => !ally.IsMe))
            {
                return ally;
            }

            if (_ward != null && _ward.IsValid && !_ward.IsDead && Player.Distance(_ward.Position) <= range)
            {
                return _ward as Obj_AI_Base;
            }

            foreach (var ward in wards)
            {
                if (Player.Distance(ward.Position) < 400)
                    continue;
                return ward;
            }

            return minions.FirstOrDefault();
        }

        private static void Wardjump()
        {
            var escapeObject = GetEscapeObject(Game.CursorPos);
            if (escapeObject != null)
            {
                if (CanCastW())
                {
                    wTimer = Utils.GameTimeTickCount + 3000;
                    _W.CastOnUnit(escapeObject);
                }
            }
            else if (_W.IsReady() && !Player.HasBuff("BlindMonkWOne"))
            {
                var wardSlot = GetWardSlot();
                if (wardSlot.IsValidSlot() &&
                    (Player.Spellbook.CanUseSpell(wardSlot.SpellSlot) == SpellState.Ready || wardSlot.Stacks != 0) &&
                    CanCastWard())
                {
                    lastWardCast = Utils.GameTimeTickCount;
                    Player.Spellbook.CastSpell(wardSlot.SpellSlot, GetCorrectedMousePosition());
                }
            }
        }

        private static void Escape()
        {
            var escapeObject = GetEscapeObject(Game.CursorPos);
            if (escapeObject != null)
            {
                if (CanCastW())
                {
                    wTimer = Utils.GameTimeTickCount + 3000;
                    _W.CastOnUnit(escapeObject);
                }
            }
        }

        private static Vector3 GetCorrectedMousePosition()
        {
            return Player.Position - (Player.Position - Game.CursorPos).Normalized()*600;
        }

        #endregion

        #region Insec

        private static Vector2 GetInsecPosition(Obj_AI_Hero target)
        {
            if (_menu.SubMenu("Insec").Item("mode").GetValue<StringList>().SelectedValue == "To Ally")
            {
                var nearestTurret =
                    ObjectManager.Get<Obj_AI_Turret>()
                        .Where(obj => obj.IsAlly)
                        .OrderBy(obj => Player.Distance(obj.Position))
                        .ToList()[0];
                var allies =
                    HeroManager.Allies.Where(hero => hero.Distance(target.Position) <= 1500)
                        .OrderByDescending(hero => hero.Distance(target.Position))
                        .ToList();
                if (allies.Any() && !allies[0].IsMe)
                {
                    var directionVector = (target.Position - allies[0].Position).Normalized().To2D();
                    return target.Position.To2D() + (directionVector*250);
                }
                var dirVector = (target.Position - nearestTurret.Position).Normalized().To2D();
                return target.Position.To2D() + (dirVector*250);
            }
            if (_menu.SubMenu("Insec").Item("mode").GetValue<StringList>().SelectedValue == "To Mouse")
            {
                var directionVector = (target.Position - Game.CursorPos).Normalized().To2D();
                return target.Position.To2D() + (directionVector*250);
            }
            var nearTurret =
                ObjectManager.Get<Obj_AI_Turret>()
                    .Where(obj => obj.IsAlly)
                    .OrderBy(obj => Player.Distance(obj.Position))
                    .ToList()[0];
            var dVector = (target.Position - nearTurret.Position).Normalized().To2D();
            return target.Position.To2D() + (dVector*250);
        }

        private static Obj_AI_Base GetInsecObject(Vector3 pos, int range = 700)
        {
            var allies =
                HeroManager.Allies.Where(hero => hero.Distance(pos) <= range)
                    .OrderBy(hero => hero.Distance(pos))
                    .ToList();
            var minions =
                MinionManager.GetMinions(pos, range, MinionTypes.All, MinionTeam.Ally)
                    .OrderBy(minion => minion.Distance(pos))
                    .ToList();
            var wards =
                ObjectManager.Get<Obj_AI_Minion>()
                    .Where(
                        obj =>
                            (obj.Name.Contains("ward") || obj.Name.Contains("Ward") || obj.Name.Contains("Trinket")) &&
                            obj.IsAlly && pos.Distance(obj.Position) <= range)
                    .OrderBy(obj => obj.Distance(pos))
                    .ToList();
            
			if (_ward != null && _ward.IsValid && !_ward.IsDead && Player.Distance(_ward.Position) <= range)
            {
                return _ward as Obj_AI_Base;
            }
			
			foreach (var ally in allies.Where(ally => !ally.IsMe))
            {
                return ally;
            }

            foreach (var minion in minions)
            {
                return minion;
            }

            return wards.FirstOrDefault();
        }

        private static Obj_AI_Base GetInsecQTarget(Obj_AI_Hero insecTarget)
        {
            if (!_menu.SubMenu("Insec").Item("qCreep").GetValue<bool>())
                return insecTarget;

            var pred = _Q.GetPrediction(insecTarget);
            if (pred.Hitchance < HitChance.Collision) return insecTarget;

            foreach (var obj in from obj in ObjectManager.Get<Obj_AI_Base>().Where(x => x.Distance(GetInsecPosition(insecTarget)) < 425 && x.IsEnemy && Player.GetSpellDamage(x, SpellSlot.Q) < x.Health) let p = _Q.GetPrediction(obj) where !p.CollisionObjects.Any() select obj)
            {
                return obj;
            }

            return insecTarget;
        }

        private static void Insec(Obj_AI_Hero target)
        {
            //Orbwalking.MoveTo(Game.CursorPos);
            if (!_R.IsReady())
                return;

            if (!target.IsValidTarget() || target.IsDead)
                return;

            var insecPos = GetInsecPosition(target);
            if (Player.Distance(insecPos) < 150)
            {
                _R.CastOnUnit(target);
                return;
            }

            switch (_menu.SubMenu("Insec").Item("method").GetValue<StringList>().SelectedValue)
            {
                case "Wardjump only":
                    if (_W.IsReady() && Player.Mana >= 50)
                    {
                        if (Player.Distance(insecPos) <= 600)
                        {
                            var insecObj = GetInsecObject(insecPos.To3D(), 200);
                            if (insecObj != null)
                            {
                                if (Player.Distance(insecObj.Position) <= 600)
                                {
                                    _W.CastOnUnit(insecObj);
                                }
                            }
                            else
                            {
                                var slot = GetWardSlot();
                                if (slot.IsValidSlot() && Player.Spellbook.CanUseSpell(slot.SpellSlot) == SpellState.Ready)
                                {
                                    Player.Spellbook.CastSpell(slot.SpellSlot, insecPos.To3D());
                                }
                                return;
                            }
                        }
                        if (!GetWardSlot().IsValidSlot())
                            return;

                        if (!_Q.IsReady())
                            return;

                        if (Player.Mana >= 130 && Player.Distance(insecPos) > 600)
                        {
                            _insecQTarget = GetInsecQTarget(target);
                            CastQCombo(target);
                        }

                        if (_target.HasBuff("BlindMonkQOne") && Player.Mana >= 80 && target.Distance(insecPos) <= 600)
                        {
                            _Q2.CastOnUnit(target);
                        }
                    }
                    break;
                case "Flash Only":
                    if (_F.Slot == SpellSlot.Unknown || !_F.IsReady())
                        return;

                    if (Player.Distance(insecPos) < 425)
                    {
                        _F.Cast(insecPos);
                        return;
                    }

                    if (!_Q.IsReady())
                        return;

                    if (Player.Mana >= 80 && Player.Distance(insecPos) > 600)
                    {
                        _insecQTarget = GetInsecQTarget(target);
                        CastQCombo(_insecQTarget);
                    }

                    if (_insecQTarget.HasBuff("BlindMonkQOne") && Player.Mana >= 80 && _insecQTarget.Distance(insecPos) <= 325)
                    {
                        _Q2.CastOnUnit(_insecQTarget);
                    }
                    break;
                default:
                    if (_W.IsReady() && Player.Mana >= 50 && (GetWardSlot().IsValidSlot() || _ward.IsValid))
                    {
                        if (Player.Distance(insecPos) <= 600)
                        {
                            var insecObj = GetInsecObject(insecPos.To3D(), 200);
                            if (insecObj != null)
                            {
                                _W.CastOnUnit(insecObj);
                            }
                            else
                            {
                                var slot = GetWardSlot();
                                if (slot.IsValidSlot() && Player.Spellbook.CanUseSpell(slot.SpellSlot) == SpellState.Ready)
                                {
                                    Player.Spellbook.CastSpell(slot.SpellSlot, insecPos.To3D());
                                }
                                return;
                            }
                        }

                        if (!GetWardSlot().IsValidSlot())
                            return;

                        if (!_Q.IsReady())
                            return;

                        if (Player.Mana >= 130 && Player.Distance(insecPos) > 600)
                        {
                            _insecQTarget = GetInsecQTarget(target);
                            CastQCombo(_insecQTarget);
                        }

                        if (_insecQTarget.HasBuff("BlindMonkQOne") && Player.Mana >= 80 && _insecQTarget.Distance(insecPos) <= 600)
                        {
                            _Q2.CastOnUnit(_insecQTarget);
                        }
                    }
                    else if (_F.Slot != SpellSlot.Unknown && _F.IsReady() && _W.IsReady())
                    {
                        var insecObj = GetInsecObject(insecPos.To3D(), 600);
                        if (insecObj != null)
                        {
                            return;
                        }
                        if (Player.Distance(insecPos) < 400 && _F.Slot != SpellSlot.Unknown && _F.IsReady())
                        {
                            _F.Cast(insecPos);
                            return;
                        }

                        if (!_Q.IsReady())
                            return;

                        if (Player.Mana >= 80 && Player.Distance(insecPos) > 600)
                        {
                            CastQCombo(GetInsecQTarget(target));
                        }

                        if (_insecQTarget.HasBuff("BlindMonkQOne") && Player.Mana >= 80 && target.Distance(insecPos) <= 325)
                        {
                            _Q2.Cast();
                        }
                    }
                    break;
            }
            
            //Orbwalking.MoveTo(Game.CursorPos);
        }

        #endregion

        #region Cast Checks

        private static void CastQCombo(Obj_AI_Base target)
        {
            switch (_menu.SubMenu("Combo").Item("qHitchance").GetValue<StringList>().SelectedIndex)
            {
                case 0:
                    _Q.CastIfHitchanceEquals(target, HitChance.Low);
                    break;
                case 1:
                    _Q.CastIfHitchanceEquals(target, HitChance.Medium);
                    break;
                case 2:
                    _Q.CastIfHitchanceEquals(target, HitChance.High);
                    break;
                case 3:
                    _Q.CastIfHitchanceEquals(target, HitChance.VeryHigh);
                    break;
            }

        }

        private static void CastQHarass(Obj_AI_Base target)
        {
            switch (_menu.SubMenu("Harass").Item("qHitchance").GetValue<StringList>().SelectedIndex)
            {
                case 0:
                    _Q.CastIfHitchanceEquals(target, HitChance.Low);
                    break;
                case 1:
                    _Q.CastIfHitchanceEquals(target, HitChance.Medium);
                    break;
                case 2:
                    _Q.CastIfHitchanceEquals(target, HitChance.High);
                    break;
                case 3:
                    _Q.CastIfHitchanceEquals(target, HitChance.VeryHigh);
                    break;
            }
        }

        private static bool CanCastW()
        {
            return _W.IsReady() && _W.Instance.Name == "BlindMonkWOne";
        }

        #endregion
    }
}