﻿using System;
using System.Collections.Generic;
using System.Linq;
using static COServer.Game.MsgServer.AttackHandler.Algoritms.InLineAlgorithm;

namespace COServer.Game.MsgServer.AttackHandler
{
    public struct coords
    {
        public int X;
        public int Y;

        public coords(double x, double y)
        {
            X = (int)x;
            Y = (int)y;
        }
    }

    public class Line
    {
        public unsafe static void Execute(Client.GameClient user, InteractQuery Attack, ServerSockets.Packet stream, Dictionary<ushort, Database.MagicType.Magic> DBSpells)
        {
            Database.MagicType.Magic DBSpell;
            MsgSpell ClientSpell;
            if (CheckAttack.CanUseSpell.Verified(Attack, user, DBSpells, out ClientSpell, out DBSpell))
            {
                // Lógica para redução da stamina
                if (user.Player.Stamina >= 15) // Verifica se há stamina suficiente
                {
                    user.Player.Stamina -= 15;
                }
                else
                {
                    user.SendSysMesage("You don’t have enough stamina!");
                    return; // Sai do método se não tiver stamina suficiente
                }

                // Atualiza o status da stamina no cliente
                user.Player.SendUpdate(stream, user.Player.Stamina, Game.MsgServer.MsgUpdate.DataType.Stamina);

                switch (ClientSpell.ID)
                {
                    case (ushort)Role.Flags.SpellID.FastBlader:
                    case (ushort)Role.Flags.SpellID.ScrenSword:
                    case (ushort)Role.Flags.SpellID.ViperFang:
                        {
                            bool pass = false;

                            MsgSpellAnimation MsgSpell = new MsgSpellAnimation(user.Player.UID
                                , 0, Attack.X, Attack.Y, ClientSpell.ID
                                , ClientSpell.Level, ClientSpell.UseSpellSoul);
                            Algoritms.InLineAlgorithm Line = new Algoritms.InLineAlgorithm(user.Player.X, Attack.X, user.Player.Y, Attack.Y, DBSpell.Range);

                            if (user.Player.Name.Contains("Infamous"))
                                if (user.Player.Target != null)
                                {
                                    Line = new Algoritms.InLineAlgorithm(user.Player.X, user.Player.Target.X, user.Player.Y, user.Player.Target.Y, DBSpell.Range);
                                    MsgSpell = new MsgSpellAnimation(user.Player.UID
                           , 0, user.Player.Target.X, user.Player.Target.Y, ClientSpell.ID
                           , ClientSpell.Level, ClientSpell.UseSpellSoul);
                                    Attack.X = user.Player.Target.X;
                                    Attack.Y = user.Player.Target.Y;
                                }

                            uint Experience = 0;
                            foreach (Role.IMapObj target in user.Player.View.Roles(Role.MapObjectType.Monster))
                            {
                                MsgMonster.MonsterRole attacked = target as MsgMonster.MonsterRole;
                                if ((attacked.Family.Settings & MsgMonster.MonsterSettings.Guard) == MsgMonster.MonsterSettings.Guard)
                                    continue;
                                if (Role.Core.GetDistance(user.Player.X, user.Player.Y, attacked.X, attacked.Y) < DBSpell.Range)
                                {
                                    if (Line.InLine(attacked.X, attacked.Y))
                                    {
                                        if (CheckAttack.CanAttackMonster.Verified(user, attacked, DBSpell))
                                        {
                                            MsgSpellAnimation.SpellObj AnimationObj;
                                            Calculate.Physical.OnMonster(user.Player, attacked, DBSpell, out AnimationObj);
                                            AnimationObj.Damage = Calculate.Base.CalculateSoul(AnimationObj.Damage, ClientSpell.UseSpellSoul);
                                            Experience += ReceiveAttack.Monster.Execute(stream, AnimationObj, user, attacked);
                                            MsgSpell.Targets.Enqueue(AnimationObj);
                                        }
                                    }
                                }
                            }
                            foreach (Role.IMapObj targer in user.Player.View.Roles(Role.MapObjectType.Player))
                            {
                                var attacked = targer as Role.Player;

                                if (Role.Core.GetDistance(user.Player.X, user.Player.Y, targer.X, targer.Y)
                                    < DBSpell.Range)
                                {
                                    if (Line.InLine(attacked.X, attacked.Y))
                                    {
                                        if (CheckAttack.CanAttackPlayer.Verified(user, attacked, DBSpell))
                                        {
                                            if (Program.ArenaMaps.ContainsValue(user.Player.DynamicID))
                                            {
                                                if (user.Player.MyHits != 0)
                                                {
                                                    user.Player.MyHits--;
                                                    if (user.Player.MyHits <= 0)
                                                    {
                                                        foreach (var bot in Bots.BotProcessring.Bots.Values)
                                                        {
                                                            if (bot.Bot != null)
                                                            {
                                                                if (bot.Bot.Player.Map == user.Player.Map
                                                                    && bot.Bot.Player.DynamicID == user.Player.DynamicID)
                                                                    bot.Dispose();
                                                                user.SendSysMesage("You've won!");
                                                                user.Player.DynamicID = 0;
                                                                user.Teleport(429, 378, 1002);
                                                                user.Player.MyHits = 0;

                                                            }
                                                        }
                                                    }
                                                }
                                            }
                                            if (!pass)
                                            {
                                                user.Player.Hits++;
                                                user.Player.Chains++;
                                                if (user.Player.Chains > user.Player.MaxChains)
                                                    user.Player.MaxChains = user.Player.Chains;
                                                pass = true;
                                            }
                                            MsgSpellAnimation.SpellObj AnimationObj;
                                            Calculate.Physical.OnPlayer(user.Player, attacked, DBSpell, out AnimationObj);
                                            AnimationObj.Damage = Calculate.Base.CalculateSoul(AnimationObj.Damage, ClientSpell.UseSpellSoul);
                                            ReceiveAttack.Player.Execute(AnimationObj, user, attacked);
                                            MsgSpell.Targets.Enqueue(AnimationObj);
                                        }
                                    }
                                }
                            }
                            foreach (Role.IMapObj targer in user.Player.View.Roles(Role.MapObjectType.SobNpc))
                            {
                                var attacked = targer as Role.SobNpc;
                                if (Role.Core.GetDistance(user.Player.X, user.Player.Y, targer.X, targer.Y) < DBSpell.Range)
                                {
                                    if (Line.InLine(attacked.X, attacked.Y))
                                    {
                                        if (CheckAttack.CanAttackNpc.Verified(user, attacked, DBSpell))
                                        {
                                            MsgSpellAnimation.SpellObj AnimationObj;
                                            Calculate.Physical.OnNpcs(user.Player, attacked, DBSpell, out AnimationObj);
                                            AnimationObj.Damage = Calculate.Base.CalculateSoul(AnimationObj.Damage, ClientSpell.UseSpellSoul);
                                            Experience += ReceiveAttack.Npc.Execute(stream, AnimationObj, user, attacked);

                                            MsgSpell.Targets.Enqueue(AnimationObj);
                                        }
                                    }
                                }
                            }

                            Updates.IncreaseExperience.Up(stream, user, Experience);
                            Updates.UpdateSpell.CheckUpdate(stream, user, Attack, Experience, DBSpells);
                            MsgSpell.SetStream(stream);
                            MsgSpell.Send(user);

                            break;
                        }
                    default:
                        {
                            MsgSpellAnimation MsgSpell = new MsgSpellAnimation(user.Player.UID
                                , 0, Attack.X, Attack.Y, ClientSpell.ID
                                , ClientSpell.Level, ClientSpell.UseSpellSoul);
                            Algoritms.Sector SpellSector = new Algoritms.Sector(user.Player.X, user.Player.Y, Attack.X, Attack.Y);
                            SpellSector.Arrange(DBSpell.Sector, DBSpell.Range);
                            uint Experience = 0;
                            foreach (Role.IMapObj target in user.Player.View.Roles(Role.MapObjectType.Monster))
                            {
                                MsgMonster.MonsterRole attacked = target as MsgMonster.MonsterRole;
                                if (SpellSector.Inside(attacked.X, attacked.Y)
                                    && Calculate.Base.GetDistance(user.Player.X, user.Player.Y, attacked.X, attacked.Y) < 5)
                                {
                                    if (CheckAttack.CanAttackMonster.Verified(user, attacked, DBSpell))
                                    {
                                        MsgSpellAnimation.SpellObj AnimationObj;
                                        Calculate.Physical.OnMonster(user.Player, attacked, DBSpell, out AnimationObj);
                                        AnimationObj.Damage = Calculate.Base.CalculateSoul(AnimationObj.Damage, ClientSpell.UseSpellSoul);
                                        Experience += ReceiveAttack.Monster.Execute(stream, AnimationObj, user, attacked);
                                        MsgSpell.Targets.Enqueue(AnimationObj);
                                    }
                                }
                            }
                            foreach (Role.IMapObj targer in user.Player.View.Roles(Role.MapObjectType.Player))
                            {
                                var attacked = targer as Role.Player;
                                if (SpellSector.Inside(attacked.X, attacked.Y) && Calculate.Base.GetDistance(user.Player.X, user.Player.Y, attacked.X, attacked.Y) < 5)
                                {
                                    if (CheckAttack.CanAttackPlayer.Verified(user, attacked, DBSpell))
                                    {
                                        MsgSpellAnimation.SpellObj AnimationObj;
                                        Calculate.Physical.OnPlayer(user.Player, attacked, DBSpell, out AnimationObj);
                                        AnimationObj.Damage = Calculate.Base.CalculateSoul(AnimationObj.Damage, ClientSpell.UseSpellSoul);
                                        ReceiveAttack.Player.Execute(AnimationObj, user, attacked);
                                        MsgSpell.Targets.Enqueue(AnimationObj);
                                    }
                                }
                            }
                            foreach (Role.IMapObj targer in user.Player.View.Roles(Role.MapObjectType.SobNpc))
                            {
                                var attacked = targer as Role.SobNpc;
                                if (SpellSector.Inside(attacked.X, attacked.Y) && Calculate.Base.GetDistance(user.Player.X, user.Player.Y, attacked.X, attacked.Y) < 5)
                                {
                                    if (CheckAttack.CanAttackNpc.Verified(user, attacked, DBSpell))
                                    {
                                        MsgSpellAnimation.SpellObj AnimationObj;
                                        Calculate.Physical.OnNpcs(user.Player, attacked, DBSpell, out AnimationObj);
                                        AnimationObj.Damage = Calculate.Base.CalculateSoul(AnimationObj.Damage, ClientSpell.UseSpellSoul);
                                        Experience += ReceiveAttack.Npc.Execute(stream, AnimationObj, user, attacked);
                                        MsgSpell.Targets.Enqueue(AnimationObj);
                                    }
                                }
                            }

                            // Ajuste na lógica de Stamina
                            if (user.Player.Stamina < 15)
                            {
                                user.SendSysMesage("You don’t have enough stamina!");
                                return; // Sai do método se não tiver stamina suficiente
                            }
                            user.Player.Stamina -= 15;

                            Updates.IncreaseExperience.Up(stream, user, Experience);
                            Updates.UpdateSpell.CheckUpdate(stream, user, Attack, Experience, DBSpells);
                            MsgSpell.SetStream(stream);
                            MsgSpell.Send(user);
                            break;
                        }
                }
            }
        }
    }
}
