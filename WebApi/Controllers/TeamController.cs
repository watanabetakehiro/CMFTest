// /////////////////////////////////////////////////////////////////////////////
// YOU CAN FREELY MODIFY THE CODE BELOW IN ORDER TO COMPLETE THE TASK
// /////////////////////////////////////////////////////////////////////////////

namespace WebApi.Controllers;

using Microsoft.AspNetCore.Mvc;
using System.Collections.Generic;
using WebApi.Helpers;
using WebApi.Entities;
using Microsoft.EntityFrameworkCore;

// JSONデータを表すクラス

public class PlayerData
{
    public string Position { get; set; }
    public int NumberOfPlayers { get; set; }
}

[ApiController]
[Route("api/[controller]")]
public class TeamController : ControllerBase
{
    private readonly DataContext Context;

    public TeamController(DataContext context)
    {
        Context = context;
    }
  
    [HttpPost("process")]
    public ActionResult<IEnumerable<Player>> ProcessTeamRequirements([FromBody] List<TeamRequirement> requirements)
    {

        if (requirements == null || requirements.Count == 0)
        {
            return BadRequest("Empty Json Value.");
        }

        // Check Inputdata(Column)
        foreach (var requirement in requirements)
        {
            var position_json = requirement.Position;
            var mainSkill_json = requirement.MainSkill;
            var numberOfPlayers_json = requirement.NumberOfPlayers;

            var result_position = Check_JsonData("position", position_json);
            if (result_position.IsError)
            {
                return BadRequest(result_position.ErrorMessage);
            }

            var result_mainSkill = Check_JsonData("mainskill", mainSkill_json);
            if (result_mainSkill.IsError)
            {
                return BadRequest(result_mainSkill.ErrorMessage);
            }

            var result_numberOfPlayers = Check_IntData("numberOfPlayers", numberOfPlayers_json);
            if (result_numberOfPlayers.IsError)
            {
                return BadRequest(result_numberOfPlayers.ErrorMessage);
            }
        }

        // Check Inputdata(Kanren)
        // position単位の人数はDBに登録してある人数以下であること
        var result_TeamRequirementPosition = requirements
            .GroupBy(rq => rq.Position)
            .Select(group => new
            {
                Position = group.Key,
                NumberOfPlayers = group.Sum(pl => pl.NumberOfPlayers)
            })
            .ToList();

        foreach (var requirement in result_TeamRequirementPosition)
        {
            var position_json = requirement.Position;
            var numberOfPlayers_json = requirement.NumberOfPlayers;

            var positionPlayer = Context.Players.Where(pl => pl.Position == position_json).ToList();
            if (positionPlayer.Count < numberOfPlayers_json) {
                return BadRequest($"Insufficient number of players for position:{position_json}.");
            }
        }

        // position単位の人数はDBに登録してある人数以下であること
        var result_CheckJsonData = requirements
            .GroupBy(rq => new {rq.Position, rq.MainSkill})
            .Select(group => new
            {
                Position = group.Key.Position,
                MainSkill = group.Key.MainSkill,
                Count = group.Count()
            })
            .ToList();

        foreach (var result in result_CheckJsonData)
        {
            if (result.Count > 1) {
                return BadRequest($"Invalid Jsondata: position:{result.Position}, mainskill:{result.MainSkill}" );
            }            
        }

        // 選択されたプレイヤーチームを格納するリスト
        var selectedPlayers = new List<Player>();
        var selectedPlayerIds = new List<int>();
        var searchTeamRequirement = new List<TeamRequirement>();
        var matchingPlayers = new List<PlayerSkill>();
        var position = "";
        var mainSkill = "";
        var numberOfPlayers = 0;

        // 1. スキルとポジションに一致するプレイヤーを選択（優先1）
        foreach (var requirement in requirements)
        {
            position = requirement.Position;
            mainSkill = requirement.MainSkill;
            numberOfPlayers = requirement.NumberOfPlayers;

            matchingPlayers = Context.PlayerSkills
                .Where(skill => skill.Skill == mainSkill && 
                    Context.Players.Any(player => player.Position == position && skill.PlayerId == player.Id) &&
                    !selectedPlayerIds.Contains(skill.PlayerId))
                .ToList();

            if (matchingPlayers.Count == numberOfPlayers)
            {
                selectedPlayerIds.AddRange(matchingPlayers.Select(player => player.PlayerId).ToList());
            } else {
                searchTeamRequirement.Add(requirement);
            }
        }

        // 1. スキルとポジションに一致するプレイヤーを選択（優先2）
        foreach (var requirement in searchTeamRequirement.ToList())
        {
            position = requirement.Position;
            mainSkill = requirement.MainSkill;
            numberOfPlayers = requirement.NumberOfPlayers;

            matchingPlayers = Context.PlayerSkills
                .Where(skill => skill.Skill == mainSkill && 
                    Context.Players.Any(player => player.Position == position && skill.PlayerId == player.Id) &&
                    !selectedPlayerIds.Contains(skill.PlayerId))
                .OrderByDescending(skill => skill.Value)
                .Take(numberOfPlayers)
                .ToList();

            if (matchingPlayers.Count > numberOfPlayers) {
                selectedPlayerIds.AddRange(matchingPlayers.Select(player => player.PlayerId).ToList());
                searchTeamRequirement.Remove(requirement);
            } else {
                selectedPlayerIds.AddRange(matchingPlayers.Select(player => player.PlayerId).ToList());
                requirement.NumberOfPlayers = numberOfPlayers - matchingPlayers.Count;
            }
        }

        // 3. ポジション一致、Skill不一致するプレイヤーを選択
        foreach (var requirement in searchTeamRequirement.ToList())
        {
            position = requirement.Position;
            mainSkill = requirement.MainSkill;
            numberOfPlayers = requirement.NumberOfPlayers;

            matchingPlayers = Context.PlayerSkills
                .Where(skill => Context.Players.Any(player => player.Position == position && skill.PlayerId == player.Id) &&
                    !selectedPlayerIds.Contains(skill.PlayerId))
                .OrderByDescending(skill => skill.Value)
                .Take(numberOfPlayers)
                .ToList();

            selectedPlayerIds.AddRange(matchingPlayers.Select(player => player.PlayerId).ToList());
            searchTeamRequirement.Remove(requirement);
        }

        selectedPlayers = Context.Players
            .Include(p => p.PlayerSkills)
            .Where(p => selectedPlayerIds.Contains(p.Id))
            .ToList();

        return selectedPlayers;
    }

    private (bool IsError, object ErrorMessage) Check_IntData(String datatype, int datavalue)
    {
        if (datatype == "numberOfPlayers" && datavalue <= 0)
        {
            var errorMessage = new { message = "Invalid value for "+ datatype + ": " + datavalue.ToString() };
            return (true, errorMessage);
        }
        return (false, null);

    }

    private (bool IsError, object ErrorMessage) Check_JsonData(String datatype, String datavalue)
    {
        // check nulldata
        if (string.IsNullOrWhiteSpace(datavalue))
        {
            var errorMessage = new { message = "Empty value for "+ datatype };
            return (true, errorMessage);
        }

        if (datatype == "position" && !new[] { "defender", "midfielder", "forward" }.Contains(datavalue))
        {
            var errorMessage_position = new { message = $"Invalid value for {datatype} : {datavalue} "};
            return (true, errorMessage_position);
        }

        if (datatype == "mainskill" && !new[] { "defense","attack","speed","strength","stamina" }.Contains(datavalue))
        {
            var errorMessage_skill = new { message = $"Invalid value for {datatype} : {datavalue} "};
            return (true, errorMessage_skill);
        }
        return (false, null);

    }
}

