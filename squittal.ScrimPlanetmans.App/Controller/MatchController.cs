using Microsoft.AspNetCore.Mvc;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using squittal.ScrimPlanetmans.ScrimMatch;
using squittal.ScrimPlanetmans.App.Services;
using squittal.ScrimPlanetmans.Models.ScrimEngine;
using Microsoft.AspNetCore.Authorization;

// For more information on enabling Web API for empty projects, visit https://go.microsoft.com/fwlink/?LinkID=397860

namespace squittal.ScrimPlanetmans.App.Controller
{
    [Route("api")]
    [ServiceFilter(typeof(IpCheckFilter))]
    [ApiController]
    public class MatchController : ControllerBase
    {
        private readonly IScrimTeamsManager _teamsManager;
        private readonly IScrimMatchEngine _matchEngine;

        public MatchController(IScrimTeamsManager teamsManager, IScrimMatchEngine matchEngine)
        {
            _teamsManager = teamsManager;
            _matchEngine = matchEngine;
        }


        // POST api/teams/{id}
        [HttpPost("teams/{id}")]
        async public void PostTeams(int id, [FromBody] string value)
        {
            var charArray = value.Split(',');
            IEnumerable<string> oldPlayers = _teamsManager.GetTeam(id).GetAllPlayerIds();

            foreach (var playerId in charArray)
            {
                if (!_teamsManager.GetTeam(id).ContainsPlayer(playerId))
                {
                    await _teamsManager.TryAddFreeTextInputCharacterToTeam(id, playerId);
                }
            }

            foreach (var playerId in oldPlayers)
            {
                if (!charArray.Contains(playerId))
                {
                    _teamsManager.SetPlayerBenchedStatus(playerId, true);
                }
            }
        }

        // POST api/title
        [HttpPost("title")]
        public void PostTitle([FromBody] string value)
        {
            _matchEngine.MatchConfiguration.TrySetTitle(value, true);
        }

        // POST api/length
        [HttpPost("length")]
        public void PostLength([FromBody] string value)
        {
            _matchEngine.MatchConfiguration.TrySetRoundLength(int.Parse(value), true);
        }

        // POST api/base
        [HttpPost("base")]
        public void PostBase([FromBody] string value)
        {
            _matchEngine.MatchConfiguration.TrySetWorldId(19, true);
            _matchEngine.MatchConfiguration.FacilityIdString = value;

        }

        // GET: api/start
        [HttpPost("start")]
        async public void PostStart([FromBody] string value)
        {
            _matchEngine.ConfigureMatch(_matchEngine.MatchConfiguration);
            await Task.Run(() => _matchEngine.Start());
        }
    }
}
