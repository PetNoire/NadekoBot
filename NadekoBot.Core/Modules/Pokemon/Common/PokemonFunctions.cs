﻿using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Discord;
using Discord.WebSocket;
using NadekoBot.Common;
using NadekoBot.Core.Services;
using NadekoBot.Core.Services.Database.Models;
using NadekoBot.Extensions;
using NadekoBot.Modules.Pokemon.Common;
using NadekoBot.Modules.Pokemon.Extentions;
using NadekoBot.Modules.Pokemon.Services;

namespace NadekoBot.Modules.Pokemon.Common
{
    static class PokemonFunctions
    {
        private static DbService _db = PokemonNew.GetDb();
        private static readonly PokemonService service = PokemonService.pokemonInstance;

        public static List<PokemonSprite> PokemonList(IUser u)
        {
            var db = _db.GetDbContext().PokemonSprite.GetAll();
            var row = db.Where(x => x.OwnerId == (long)u.Id);

            if (row.Count() >= (u.IsBot ? 1 : 6))
            {
                return row.ToList();
            }
            else
            {

                var list = new List<PokemonSprite>();
                while (row.Count() + list.Count < (u.IsBot ? 1 : 6))
                {
                    var pkm = GeneratePokemon(u);
                    if (!list.Where(x => x.IsActive).Any())
                    {
                        pkm.IsActive = true;
                    }

                    list.Add(pkm);


                }
                //Set an active pokemon
                var uow = _db.GetDbContext();
                uow.PokemonSprite.AddRange(list.ToArray());
                uow.SaveChangesAsync();

                return list;
            }
        }

        private static NadekoRandom rng = new NadekoRandom();
        public static  PokemonSprite GeneratePokemon(IUser u)
        {
            var list = service.pokemonClasses.Where(x => x.EvolveStage == 0).ToList();
            var speciesIndex = rng.Next(0, list.Count() - 1);
            rng.Next();
            var species = list[speciesIndex];
            PokemonSprite sprite = new PokemonSprite
            {
                SpeciesId = species.ID,
                HP = species.BaseStats["hp"],
                Level = 1,
                NickName = species.Name,
                OwnerId = (long)u.Id,
                XP = 0,
                IsShiny = rng.Next(0, 65536) < 8,
                Attack = species.BaseStats["attack"],
                Defense = species.BaseStats["defense"],
                SpecialAttack = species.BaseStats["special-attack"],
                SpecialDefense = species.BaseStats["special-defense"],
                Speed = species.BaseStats["speed"],
                MaxHP = species.BaseStats["hp"]
            };

            while (sprite.Level < 4)
            {
                sprite.LevelUp();
            }
            sprite.XP = sprite.XPRequired();
            sprite.LevelUp();
            var moves = GetNewPkmMoves(sprite.SpeciesId);
            switch (moves.Count())
            {
                case 4:
                    sprite.Move4 = moves[3].Name;
                    goto case 3;
                case 3:
                    sprite.Move3 = moves[2].Name;
                    goto case 2;
                case 2:
                    sprite.Move2 = moves[1].Name;
                    goto case 1;
                case 1:
                    sprite.Move1 = moves[0].Name;
                    break;
            }
            return sprite;
        }

        public static MoveList GetNewPkmMoves(int id)
        {
            var pkm = service.pokemonClasses[id];
            var learnmoves = service.pokemonClasses[id].LearnSet.Where(x => x.LearnLevel <= 5).Shuffle().Take(4).ToList();
            var moveList = new MoveList();
            foreach (var move in learnmoves)
            {
                var pkmMove = service.pokemonMoves.Where(x => x.ID == move.ID).First();
                moveList.AddIfNotNull(pkmMove);
            }
            return moveList;
        }
        public static async void UpdatePokemon(PokemonSprite pokemon)
        {
            var uow = _db.GetDbContext();
            uow.PokemonSprite.Update(pokemon);
            await uow.SaveChangesAsync();
        }

        public static async Task<PokemonMove> GetMoveAsync(PokemonSprite pokemon, string move) =>
            await Task.Run(() => {
                var moves = GetMoves(pokemon).Where(x => x.Name == move);
                if (moves.Count() > 0)
                    return moves.First();
                else
                    return null;
            });

        public static async Task<MoveList> GetMovesAsync(PokemonSprite pokemon) =>
            await Task.Run(() => GetMoves(pokemon));

        public static MoveList GetMoves(PokemonSprite pokemon)
        {
            var moves = new MoveList();
            moves.AddIfNotNull(service.pokemonMoves[pokemon.Move1]);
            moves.AddIfNotNull(service.pokemonMoves[pokemon.Move2]);
            moves.AddIfNotNull(service.pokemonMoves[pokemon.Move3]);
            moves.AddIfNotNull(service.pokemonMoves[pokemon.Move4]);
            
            return moves;
        }

        public static async void DeletePokemon(PokemonSprite pokemon)
        {
            var uow = _db.GetDbContext();
            uow.PokemonSprite.Remove(pokemon);
            await uow.SaveChangesAsync();
        }

        public static async Task<List<PokemonTrainer>> GetTopPlayersAsync() =>
            await Task.Run(() => { return GetTopPlayers(); });

        public static List<PokemonTrainer> GetTopPlayers()
        {
            var db = _db.GetDbContext().PokemonSprite.GetAll();
            var users = db.DistinctBy(x => x.OwnerId);
            var output = new List<PokemonTrainer>();
            foreach (var user in users)
            {
                var usrpkm = db.Where(x => x.OwnerId == user.OwnerId);
                long exp = 0;
                foreach (var pkm in usrpkm)
                    exp += pkm.XP;
                var trainer = new PokemonTrainer()
                {
                    ID = user.OwnerId,
                    TotalExp = exp,
                    TopPokemon = usrpkm.OrderByDescending(x => x.Level).First()
                };
                output.Add(trainer);
            }
            output = output.OrderByDescending(x => x.TotalExp).Take(4).ToList();
            return output;
        }

        public static async Task<List<PokemonTrainer>> GetPlayerRankAsync(IUser player) =>
            await Task.Run(() => { return GetPlayerRank(player); });

        public static List<PokemonTrainer> GetPlayerRank(IUser player)
        {
            var db = _db.GetDbContext().PokemonSprite.GetAll();
            var users = db.DistinctBy(x => x.OwnerId);
            var output = new List<PokemonTrainer>();
            //int player 
            foreach (var user in users)
            {
                var usrpkm = db.Where(x => x.OwnerId == user.OwnerId);
                long exp = 0;
                foreach (var pkm in usrpkm)
                    exp += pkm.XP;
                var trainer = new PokemonTrainer()
                {
                    ID = user.OwnerId,
                    TotalExp = exp,
                    TopPokemon = usrpkm.OrderByDescending(x => x.Level).First()
                };
                output.Add(trainer);
            }
            output = output.OrderByDescending(x => x.TotalExp).ToList();
            for (int i = 0; i < output.Count; i++)
                output[i].Rank = i + 1;
            var playerRank = output.Where(x => x.ID == (long)player.Id).First().Rank;
            output = output.Skip(playerRank - 3).Take(5).ToList();
            return output;
        }

        public static PokemonSprite GetActivePokemon(IUser user)
        {
            var list = PokemonList(user);
            var active = list.Where(x => x.IsActive).FirstOrDefault();
            if (active == null)
            {
                var pkm = list.Where(x => x.HP > 0).FirstOrDefault() ?? list.First();
                pkm.IsActive = true;
                UpdatePokemon(pkm);
                active = pkm;
            }
            return active;
        }

        public enum AttackResult
        {
            InvalidMove,
            AlreadyAttacked,
            AttackerIsDead,
            DefenderIsDead,

        }

        public static AttackResult DoAttack(PokemonSprite Attacker, PokemonSprite Defender, String moveString)
        {
            throw (new NotImplementedException());

            //var species = Attacker.GetSpecies();
            //if (!species.Moves.Keys.Contains(moveString.Trim()))
            //{
            //    return AttackResult.InvalidMove;
            //}

            //var target = Defender.GetOwner();
            //var attackerStats = Attacker.GetOwner().GetTrainerStats();
            //var defenderStats = target.GetTrainerStats();
            //if (attackerStats.MovesMade > TrainerStats.MaxMoves || attackerStats.LastAttacked.Contains(target.Id))
            //    return AttackResult.AlreadyAttacked;
            
            //if (Attacker.HP == 0)
            //    return AttackResult.AttackerIsDead;
            
            //if (defenderStats.LastAttackedBy.ContainsKey(Context.Guild.Id))
            //    defenderStats.LastAttackedBy.Remove(Context.Guild.Id);
            //defenderStats.LastAttackedBy.Add(Context.Guild.Id, attacker);
            //KeyValuePair<string, string> move = new KeyValuePair<string, string>(moveString, species.Moves[moveString]);

            //if (Defender.HP == 0)
            //    return AttackResult.DefenderIsDead;

            //PokemonAttack attack = new PokemonAttack(Attacker, Defender, move);
            //var msg = attack.AttackString();

            //Defender.HP -= attack.Damage;
            //msg += $"{Defender.NickName} has {Defender.HP} HP left!";
            //await ReplyAsync(msg);
            ////Update stats, you shall
            //Attacker.GetOwner().UpdateTrainerStats(attackerStats.Attack(target));
            //target.UpdateTrainerStats(defenderStats.Reset());
            //Attacker.Update();
            //Defender.Update();

            //if (Defender.HP <= 0)
            //{

            //    var str = $"{Defender.NickName} fainted!\n" + (!target.IsBot ? $"{Attacker.NickName}'s owner {Attacker.GetOwner().Mention} receives 1 {_bc.BotConfig.CurrencySign}\n" : "");
            //    var lvl = Attacker.Level;
            //    if (!target.IsBot)
            //    {
            //        var extraXP = Attacker.Reward(Defender);
            //        str += $"{Attacker.NickName} gained {extraXP} XP from the battle\n";
            //    }
            //    if (Attacker.Level > lvl) //levled up
            //    {
            //        str += $"**{Attacker.NickName}** leveled up!\n**{Attacker.NickName}** is now level **{Attacker.Level}**";
            //        //Check evostatus
            //    }
            //    Attacker.Update();
            //    Defender.Update();
            //    var list = target.GetPokemon().Where(s => (s.HP > 0 && s != Defender));
            //    if (list.Any())
            //    {
            //        var toSet = list.FirstOrDefault();
            //        switch (SwitchPokemon(target, toSet))
            //        {
            //            case SwitchResult.Pass:
            //                {
            //                    str += $"\n{target.Mention}'s active pokemon set to **{toSet.NickName}**";
            //                    break;
            //                }
            //            case SwitchResult.Failed:
            //            case SwitchResult.TargetFainted:
            //                {
            //                    str += $"\n **Error:** could not switch pokemon";
            //                    break;
            //                }
            //        }
            //    }
            //    else
            //    {
            //        str += $"\n{target.Mention} has no pokemon left!";
            //        if (target.IsBot)
            //        {
            //            var pkmlist = target.GetPokemon();
            //            foreach (var pkm in pkmlist)
            //                pkm.Heal();
            //        }
            //        //do something?
            //    }
            //    //UpdatePokemon(attackerPokemon);
            //    //UpdatePokemon(defenderPokemon);
            //    await ReplyAsync(str);
            //    if (!target.IsBot)
            //        await _cs.AddAsync(attacker.Id, "Victorious in pokemon", 1);

            //}
            //if (target.IsBot)
            //{
            //    await DoAttack(target, attacker, target.ActivePokemon().GetSpecies().Moves.Keys.ElementAt((new Random().Next(3))));

            //}
        }

        public enum SwitchResult
        {
            Pass,
            Failed,
            TargetFainted,

        }
        /// <summary>
        /// Sets the active pokemon of the given user to the given Sprite
        /// </summary>
        /// <param name="u"></param>
        /// <param name="newActive"></param>
        /// <returns></returns>
        public static SwitchResult SwitchPokemon(IUser user, PokemonSprite newActive)
        {
            var toUnset = user.GetPokemon().Where(x => x.IsActive).FirstOrDefault();
            if (toUnset == null)
            {
                return SwitchResult.Failed;
            }
            if (newActive.HP <= 0)
            {
                return SwitchResult.TargetFainted;
            }
            toUnset.IsActive = false;
            newActive.IsActive = true;
            toUnset.Update();
            newActive.Update();

            return SwitchResult.Pass;
        }
    }
}
