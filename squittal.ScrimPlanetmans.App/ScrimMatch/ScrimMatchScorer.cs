﻿using Microsoft.Extensions.Logging;
using squittal.ScrimPlanetmans.Shared.Models.Planetside.Events;
using squittal.ScrimPlanetmans.Shared.Models;
using System;
using System.Linq;
using squittal.ScrimPlanetmans.ScrimMatch.Models;
using System.Threading.Tasks;
using squittal.ScrimPlanetmans.Services.ScrimMatch;
using squittal.ScrimPlanetmans.ScrimMatch.Messages;

namespace squittal.ScrimPlanetmans.ScrimMatch
{
    public class ScrimMatchScorer : IScrimMatchScorer
    {
        private readonly IScrimRulesetManager _rulesets;
        private readonly IScrimTeamsManager _teamsManager;
        private readonly IScrimMessageBroadcastService _messageService;
        private readonly ILogger<ScrimMatchEngine> _logger;

        private Ruleset _activeRuleset;

        // Variables for FacilityControl logic
        private int _currentRound { get; set; } = 0;
        private bool _wasFirstBaseControlDefense { get; set; } = false;

        public ScrimMatchScorer(IScrimRulesetManager rulesets, IScrimTeamsManager teamsManager, IScrimMessageBroadcastService messageService, ILogger<ScrimMatchEngine> logger)
        {
            _rulesets = rulesets;
            _teamsManager = teamsManager;
            _messageService = messageService;
            _logger = logger;

            _messageService.RaiseMatchStateUpdateEvent += 
            //_activeRuleset = _rulesets.GetActiveRuleset();
        }

        #region Death Events
        public async Task SetActiveRuleset()
        {
            //_activeRuleset = await _rulesets.GetDefaultRuleset();
            _activeRuleset = await _rulesets.GetActiveRuleset();
        }

        public int ScoreDeathEvent(ScrimDeathActionEvent death)
        {
            switch (death.DeathType)
            {
                case DeathEventType.Kill:
                    return ScoreKill(death);

                case DeathEventType.Suicide:
                    return ScoreSuicide(death);

                case DeathEventType.Teamkill:
                    return ScoreTeamkill(death);

                default:
                    return 0;
            }
        }

        private int ScoreKill(ScrimDeathActionEvent death)
        {
            int points;

            if (death.ActionType == ScrimActionType.InfantryKillInfantry)
            {
                var categoryId = death.Weapon.ItemCategoryId;
                points = _activeRuleset.ItemCategoryRules
                                            .Where(rule => rule.ItemCategoryId == categoryId)
                                            .Select(rule => rule.Points)
                                            .FirstOrDefault();
            }
            else
            {
                var actionType = death.ActionType;
                points = GetActionRulePoints(actionType);
                //points = _activeRuleset.ActionRules
                //                            .Where(rule => rule.ScrimActionType == actionType)
                //                            .Select(rule => rule.Points)
                //                            .FirstOrDefault();
            }

            var isHeadshot = (death.IsHeadshot ? 1 : 0);

            var attackerUpdate = new ScrimEventAggregate()
            {
                Points = points,
                NetScore = points,
                Kills = 1,
                Headshots = isHeadshot
            };

            var victimUpdate = new ScrimEventAggregate()
            {
                NetScore = -points,
                Deaths = 1,
                HeadshotDeaths = isHeadshot
            };

            // Player Stats update automatically updates the appropriate team's stats
            _teamsManager.UpdatePlayerStats(death.AttackerPlayer.Id, attackerUpdate);
            _teamsManager.UpdatePlayerStats(death.VictimPlayer.Id, victimUpdate);

            return points;
        }

        private int ScoreSuicide(ScrimDeathActionEvent death)
        {
            var actionType = death.ActionType;
            var points = GetActionRulePoints(actionType);
            //var points = _activeRuleset.ActionRules
            //                            .Where(rule => rule.ScrimActionType == actionType)
            //                            .Select(rule => rule.Points)
            //                            .FirstOrDefault();

            var victimUpdate = new ScrimEventAggregate()
            {
                Points = points,
                NetScore = points,
                Deaths = 1,
                Suicides = 1
            };

            // Player Stats update automatically updates the appropriate team's stats
            _teamsManager.UpdatePlayerStats(death.VictimPlayer.Id, victimUpdate);

            return points;
        }

        private int ScoreTeamkill(ScrimDeathActionEvent death)
        {
            var actionType = death.ActionType;
            var points = GetActionRulePoints(actionType);
            //var points = _activeRuleset.ActionRules
            //                            .Where(rule => rule.ScrimActionType == actionType)
            //                            .Select(rule => rule.Points)
            //                            .FirstOrDefault();

            var attackerUpdate = new ScrimEventAggregate()
            {
                Points = points,
                NetScore = points,
                Teamkills = 1
            };

            var victimUpdate = new ScrimEventAggregate()
            {
                Deaths = 1,
                TeamkillDeaths = 1
            };

            // Player Stats update automatically updates the appropriate team's stats
            _teamsManager.UpdatePlayerStats(death.AttackerPlayer.Id, attackerUpdate);
            _teamsManager.UpdatePlayerStats(death.VictimPlayer.Id, victimUpdate);

            return points;
        }

        public int ScoreDeathEvent(Death death)
        {
            var attackerId = death.AttackerCharacterId;
            var victimId = death.CharacterId;

            bool onSameTeam = _teamsManager.DoPlayersShareTeam(attackerId, victimId, out int? attackerTeamOrdinal, out int? victimTeamOrdinal);

            death.AttackerTeamOrdinal = attackerTeamOrdinal;
            death.CharacterTeamOrdinal = victimTeamOrdinal;

            death.DeathEventType = onSameTeam ? DeathEventType.Suicide : death.DeathEventType;

            switch (death.DeathEventType)
            {
                case DeathEventType.Kill:
                    return ScoreKill(death);

                case DeathEventType.Suicide:
                    return ScoreSuicide(death);

                case DeathEventType.Teamkill:
                    return ScoreTeamkill(death);

                default:
                    return 0;
            }
        }

        private int ScoreKill(Death death)
        {
            int points = 2;
            int headshot = (death.IsHeadshot ? 1 : 0);

            // Attacker Points
            if (death.AttackerTeamOrdinal != null)
            {
                var attackerAggregate = new ScrimEventAggregate()
                {
                    Points = points,
                    NetScore = points,
                    Kills = 1,
                    Headshots = headshot
                };

                // Player Stats update automatically updates the appropriate team's stats
                _teamsManager.UpdatePlayerStats(death.AttackerCharacterId, attackerAggregate);
            }

            // Victim Points
            if (death.CharacterTeamOrdinal != null)
            {
                var victimAggregate = new ScrimEventAggregate()
                {
                    NetScore = -points,
                    Deaths = 1,
                    HeadshotDeaths = headshot
                };

                // Player Stats update automatically updates the appropriate team's stats
                _teamsManager.UpdatePlayerStats(death.CharacterId, victimAggregate);
            }

            return points;
        }

        private int ScoreSuicide(Death death)
        {
            int points = -3;
            int headshot = (death.IsHeadshot ? 1 : 0);

            // Victim Points
            if (death.CharacterTeamOrdinal != null)
            {
                var victimAggregate = new ScrimEventAggregate()
                {
                    Points = points,
                    NetScore = points,
                    Deaths = 1,
                    Suicides = 1,
                    HeadshotDeaths = headshot
                };

                // Player Stats update automatically updates the appropriate team's stats
                _teamsManager.UpdatePlayerStats(death.CharacterId, victimAggregate);
            }

            return points;
        }

        private int ScoreTeamkill(Death death)
        {
            int points = -3;
            //int headshot = (death.IsHeadshot ? 1 : 0);

            // Attacker Points
            if (death.AttackerTeamOrdinal != null)
            {
                var attackerAggregate = new ScrimEventAggregate()
                {
                    Points = points,
                    NetScore = points,
                    Teamkills = 1
                };

                // Player Stats update automatically updates the appropriate team's stats
                _teamsManager.UpdatePlayerStats(death.AttackerCharacterId, attackerAggregate);
            }

            // Victim Points
            if (death.CharacterTeamOrdinal != null)
            {
                var victimAggregate = new ScrimEventAggregate()
                {
                    Deaths = 1,
                    TeamkillDeaths = 1
                };

                // Player Stats update automatically updates the appropriate team's stats
                _teamsManager.UpdatePlayerStats(death.CharacterId, victimAggregate);
            }

            return points;
        }

        #endregion Death Events

        #region Experience Events
        public int ScoreGainExperienceEvent(GainExperience expGain)
        {
            throw new NotImplementedException();
        }

        public int ScoreReviveEvent(ScrimReviveActionEvent revive)
        {
            var actionType = revive.ActionType;
            var points = GetActionRulePoints(actionType);

            var medicUpdate = new ScrimEventAggregate()
            {
                Points = points,
                NetScore = points,
                RevivesGiven = 1
            };

            var revivedUpdate = new ScrimEventAggregate()
            {
                RevivesTaken = 1
            };

            // Player Stats update automatically updates the appropriate team's stats
            _teamsManager.UpdatePlayerStats(revive.MedicPlayer.Id, medicUpdate);
            _teamsManager.UpdatePlayerStats(revive.RevivedPlayer.Id, revivedUpdate);

            return points;
        }

        public int ScoreAssistEvent(ScrimAssistActionEvent assist)
        {
            var actionType = assist.ActionType;
            var points = GetActionRulePoints(actionType);

            var attackerUpdate = new ScrimEventAggregate()
            {
                Points = points,
                NetScore = points
            };

            var victimUpdate = new ScrimEventAggregate();

            if (actionType == ScrimActionType.DamageAssist)
            {
                attackerUpdate.DamageAssists = 1;
                victimUpdate.DamageAssistedDeaths = 1;
            }
            else
            {
                attackerUpdate.UtilityAssists = 1;
                victimUpdate.UtilityAssistedDeaths = 1;
            }

            // Player Stats update automatically updates the appropriate team's stats
            _teamsManager.UpdatePlayerStats(assist.AttackerPlayer.Id, attackerUpdate);
            _teamsManager.UpdatePlayerStats(assist.VictimPlayer.Id, victimUpdate);

            return points;
        }

        public int ScoreObjectiveTickEvent(ScrimObjectiveTickActionEvent objective)
        {
            var actionType = objective.ActionType;
            var points = _activeRuleset.ActionRules
                                        .Where(rule => rule.ScrimActionType == actionType)
                                        .Select(rule => rule.Points)
                                        .FirstOrDefault();

            var playerUpdate = new ScrimEventAggregate()
            {
                Points = points,
                NetScore = points
            };

            var isDefense = (actionType == ScrimActionType.PointDefend
                                || actionType == ScrimActionType.ObjectiveDefensePulse);

            if (isDefense)
            {
                playerUpdate.ObjectiveDefenseTicks = 1;
            }
            else
            {
                playerUpdate.ObjectiveCaptureTicks = 1;
            }

            // Player Stats update automatically updates the appropriate team's stats
            _teamsManager.UpdatePlayerStats(objective.Player.Id, playerUpdate);

            return points;
        }

        #endregion Experience Events

        #region Objective Events
        public int ScoreFacilityControlEvent(FacilityControl control, out bool controlCounts)
        {
            var teamOrdinal = control.ControllingTeamOrdinal;
            var type = control.Type;

            var team = _teamsManager.GetTeam(teamOrdinal);

            if (!DoesFacilityControlCount(type, teamOrdinal))
            {
                controlCounts = false;
                return 0;
            }
            else
            {
                controlCounts = true;
            }

            var roundControlVictories = team.EventAggregateTracker.RoundStats.BaseControlVictories;

            var actionType = (roundControlVictories == 0)
                                ? ScrimActionType.FirstBaseCapture
                                : ScrimActionType.SubsequentBaseCapture;

            var points = GetActionRulePoints(actionType);

            var teamUpdate = new ScrimEventAggregate()
            {
                Points = points,
                NetScore = points,
                BaseCaptures = (type == FacilityControlType.Capture ? 1 : 0),
                BaseDefenses = (type == FacilityControlType.Defense ? 1 : 0)
            };

            _teamsManager.UpdateTeamStats(teamOrdinal, teamUpdate);
            
            return points;
        }

        private bool DoesFacilityControlCount(FacilityControlType type, int teamOrdinal)
        {
            var team = _teamsManager.GetTeam(teamOrdinal);

            var roundControlVictories = team.EventAggregateTracker.RoundStats.BaseControlVictories;

            if (roundControlVictories == 0)
            {
                return true;
            }

            var previousScoredControlType = team.EventAggregateTracker.RoundStats.PreviousScoredBaseControlType;

            return (type != previousScoredControlType);

            /*
            var roundDefenses = team.EventAggregateTracker.RoundStats.BaseDefenses;
            if (type == FacilityControlType.Defense)
            {
                return roundDefenses == 0
            }


            var roundCaptures = team.EventAggregateTracker.RoundStats.BaseCaptures;
            */
        }

        public int ScorePlayerFacilityCaptureEvent(PlayerFacilityCapture capture)
        {
            throw new NotImplementedException();
        }

        public int ScorePlayerFacilityDefendEvent(PlayerFacilityDefend defense)
        {
            throw new NotImplementedException();
        }

        #endregion Objective Events

        #region Misc. Non-Scored Events
        public void HandlePlayerLogin(PlayerLogin login)
        {
            var characterId = login.CharacterId;
            _teamsManager.SetPlayerOnlineStatus(characterId, true);
        }

        public void HandlePlayerLogout(PlayerLogout login)
        {
            var characterId = login.CharacterId;
            _teamsManager.SetPlayerOnlineStatus(characterId, false);
        }
        #endregion Misc. Non-Scored Events
    
        private int GetActionRulePoints(ScrimActionType actionType)
        {
            return _activeRuleset.ActionRules
                                    .Where(rule => rule.ScrimActionType == actionType)
                                    .Select(rule => rule.Points)
                                    .FirstOrDefault();
        }

        #region Message Handling
        private void OnMatchStateUpdateEvent(object sender, MatchStateUpdateEventArgs e)
        {
            _currentRound = e.Message.CurrentRound;
        }
        #endregion

    }
}
