﻿using squittal.ScrimPlanetmans.ScrimMatch.Models;
using squittal.ScrimPlanetmans.Services.Planetside;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace squittal.ScrimPlanetmans.App.Services
{
    public class ScrimPlayersService
    {
        private readonly IOutfitService _outfits;
        private readonly ICharacterService _characters;

        public ScrimPlayersService(IOutfitService outfits, ICharacterService characters)
        {
            _outfits = outfits;
            _characters = characters;
        }
        public async Task<Player> GetPlayerFromCharacterId(string characterId)
        {
            var character = await _characters.GetCharacterAsync(characterId);

            if (character == null)
            {
                return null;
            }

            return new Player(character);
        }

        public async Task<IEnumerable<Player>> GetPlayersFromOutfitAlias(string alias)
        {
            var censusMembers = await _outfits.GetOutfitMembersByAlias(alias);

            if (censusMembers == null || !censusMembers.Any())
            {
                return null;
            }

            return censusMembers.Select(m => new Player(m)).ToList();
        }
    }
}
