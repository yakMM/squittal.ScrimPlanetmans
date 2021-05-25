using Microsoft.AspNetCore.Mvc;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using squittal.ScrimPlanetmans.ScrimMatch;
using squittal.ScrimPlanetmans.App.Services;
using squittal.ScrimPlanetmans.Models.ScrimEngine;
using Microsoft.AspNetCore.Authorization;
using squittal.ScrimPlanetmans.ScrimMatch.Messages;
using squittal.ScrimPlanetmans.Services.ScrimMatch;

namespace squittal.ScrimPlanetmans.App.Controller
{
    [Route("api")]
    [ServiceFilter(typeof(IpCheckFilter))]
    [ApiController]
    public class MatchController : ControllerBase
    {
        private IScrimTeamsManager _teamsManager;
        private IScrimMatchEngine _matchEngine;
        private IScrimMessageBroadcastService _messageService;
        private MatchConfiguration _configuration;

        public MatchController(IScrimTeamsManager teamsManager, IScrimMatchEngine matchEngine, IScrimMessageBroadcastService messageService)
        {
            _teamsManager = teamsManager;
            _matchEngine = matchEngine;
            _messageService = messageService;
            _configuration = matchEngine.MatchConfiguration;
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
        public void PostTitle([FromBody] string newTitle)
        {
            var oldTitle = _configuration.Title;
            if (_configuration.TrySetTitle(newTitle, true))
            {
                if (newTitle != oldTitle)
                {
                    _configuration.Title = newTitle;
                    _messageService.BroadcastMatchConfigurationUpdateMessage(new MatchConfigurationUpdateMessage(_configuration));
                }

            }
        }

        // POST api/length
        [HttpPost("length")]
        public void PostLength([FromBody] string value)
        {
            var newValue = int.Parse(value);
            var oldValue = _configuration.RoundSecondsTotal;
            if (_configuration.TrySetRoundLength(newValue, true))
            {
                if (newValue != oldValue)
                {
                    _configuration.RoundSecondsTotal = newValue;
                    _messageService.BroadcastMatchConfigurationUpdateMessage(new MatchConfigurationUpdateMessage(_configuration));
                }

            }
        }

        // POST api/base
        [HttpPost("base")]
        public void PostBase([FromBody] string newBase)
        {
            var oldBase = _configuration.FacilityIdString;
            if (_configuration.TrySetWorldId(19, true, false))
            {
                if (oldBase != newBase)
                {
                    _configuration.FacilityIdString = newBase;
                    _messageService.BroadcastMatchConfigurationUpdateMessage(new MatchConfigurationUpdateMessage(_configuration));
                }

            }
        }

        // POST: api/start
        [HttpPost("start")]
        async public void PostStart()
        {
            _matchEngine.ConfigureMatch(_matchEngine.MatchConfiguration);
            await Task.Run(() => _matchEngine.Start());
        }

        // POST: api/clear
        [HttpPost("clear")]
        async public void PostClear()
        {
            await Task.Run(() => _matchEngine.ClearMatch(false));
        }
    }
}
