// /////////////////////////////////////////////////////////////////////////////
// YOU CAN FREELY MODIFY THE CODE BELOW IN ORDER TO COMPLETE THE TASK
// /////////////////////////////////////////////////////////////////////////////

namespace WebApi.Controllers;

using Microsoft.AspNetCore.Mvc;
using System.Collections.Generic;
using System.Threading.Tasks;
using WebApi.Helpers;
using WebApi.Entities;
using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.AspNetCore.Authorization;

[ApiController]
[Route("api/[controller]")]
public class PlayerController : ControllerBase
{
  private readonly DataContext Context;

  public PlayerController(DataContext context)
  {
    Context = context;
  }

  //ListofPlayers
  [HttpGet]
  public async Task<ActionResult<IEnumerable<Player>>> GetAll()
  {
    var players = await Context.Players.Include(p => p.PlayerSkills).ToListAsync();
    return Ok(players); 
  }

  //Create Player
  [HttpPost]
  public async Task<ActionResult<Player>> PostPlayer([FromBody] JsonElement data)
  {

    try 
    {
      //get jsondata
      String json_name = data.TryGetProperty("name", out var nameElement) ? nameElement.GetString() : null;
      var result_name = Check_JsonData("name",json_name);
      if (result_name.IsError)
      {
        return BadRequest(result_name.ErrorMessage);
      }

      String json_position = data.TryGetProperty("position", out var positionElement) ? positionElement.GetString() : null;
      var result_position = Check_JsonData("position", json_position);
      if (result_position.IsError)
      {
        return BadRequest(result_position.ErrorMessage);
      }

      var playerSkills = new List<PlayerSkill>();
      var playerSkillsData = data.GetProperty("playerSkills");
      foreach (var skillData in playerSkillsData.EnumerateArray())
      {
        string skill = skillData.TryGetProperty("skill", out var skillElement) ? skillElement.GetString() : null;
        var result_skill = Check_JsonData("skill", skill);
        if (result_skill.IsError)
        {
          return BadRequest(result_skill.ErrorMessage);
        }

        int value = skillData.TryGetProperty("value", out var valueElement) ? valueElement.GetInt32() : 0;
        playerSkills.Add(new PlayerSkill { Skill = skill, Value = value });

      }
      if (playerSkills.Count == 0) 
      {
        return BadRequest(new { message = "Empty value for skill" });
      }

      // position�P�ʂ̐l����DB�ɓo�^���Ă���l���ȉ��ł��邱��
      var result_CheckJsonData = playerSkills
          .GroupBy(rq => new {rq.Skill})
          .Select(group => new
          {
              Skill = group.Key.Skill,
              Count = group.Count()
          })
          .ToList();

      foreach (var result in result_CheckJsonData)
      {
          if (result.Count > 1) {
              return BadRequest("Invalid Jsondata: Duplicate Skill:"  + result.Skill + "." );
          }            
      }

      //Database insert
      Player player = new Player {Name = json_name, Position = json_position, PlayerSkills = playerSkills};
      
      Context.Players.Add(player);
      await Context.SaveChangesAsync();

      return CreatedAtAction("GetPlayerById", new { id = player.Id }, player);
    }
    catch (Exception ex)
    {
      var errorMessage = new { message = ex.Message };
      return BadRequest(errorMessage);
    }

  }

  [HttpGet("{id}", Name = "GetPlayerById")]
  public async Task<Player> GetPlayerById(int id)
  {
      var player = await Context.Players.FindAsync(id);
      return player;
  }


  [HttpPut("{id}")]
  public async Task<ActionResult<Player>> PutPlayer(int id, [FromBody] JsonElement data)
  {
    var playerToUpdate = await GetPlayerById(id);
    if (playerToUpdate == null)
    {
      return BadRequest(new { message = "Not Found Player. Id:" + id.ToString() });
    }

    try
    {
      // ?v???C???[??v???p?e?B???X?V
      String json_name = data.TryGetProperty("name", out var nameElement) ? nameElement.GetString() : null;
      var result_name = Check_JsonData("name", json_name);
      if (result_name.IsError)
      {
        return BadRequest(result_name.ErrorMessage);
      }
      playerToUpdate.Name = json_name;

      String json_position = data.TryGetProperty("position", out var positionElement) ? positionElement.GetString() : null;
      var result_position = Check_JsonData("position", json_position);
      if (result_position.IsError)
      {
        return BadRequest(result_position.ErrorMessage);
      }
      playerToUpdate.Position = json_position;

      // ?v???C???[?X?L????X?V
      var playerSkills = new List<PlayerSkill>();
      var playerSkillsData = data.GetProperty("playerSkills");
      foreach (var skillData in playerSkillsData.EnumerateArray())
      {
        string skill = skillData.TryGetProperty("skill", out var skillElement) ? skillElement.GetString() : null;
        var result_skill = Check_JsonData("skill", skill);
        if (result_skill.IsError)
        {
          return BadRequest(result_skill.ErrorMessage);
        }


        int value = skillData.TryGetProperty("value", out var valueElement) ? valueElement.GetInt32() : 0;
        playerSkills.Add(new PlayerSkill { Skill = skill, Value = value });

      }

      // position�P�ʂ̐l����DB�ɓo�^���Ă���l���ȉ��ł��邱��
      var result_CheckJsonData = playerSkills
          .GroupBy(rq => new {rq.Skill})
          .Select(group => new
          {
              Skill = group.Key.Skill,
              Count = group.Count()
          })
          .ToList();

      foreach (var result in result_CheckJsonData)
      {
          if (result.Count > 1) {
              return BadRequest("Invalid Jsondata: Duplicate Skill:"  + result.Skill + "." );
          }            
      }

      var playerskillToUpdate = await Context.PlayerSkills.Where(ps => ps.PlayerId == id).ToListAsync();
      Context.PlayerSkills.RemoveRange(playerskillToUpdate);
      playerToUpdate.PlayerSkills = playerSkills;
      await Context.SaveChangesAsync();

      return CreatedAtAction("GetPlayerById", new { id = playerToUpdate.Id }, playerToUpdate);
    }
    catch (Exception ex)
    {
      var errorMessage = new { message = ex.Message };
      return BadRequest(errorMessage);
    }
  }

  [HttpDelete("{id}")]
//  [Authorize]
  public async Task<ActionResult<Player>> DeletePlayer(int id)
  {
    var playerToDelete = await GetPlayerById(id);
    if (playerToDelete == null)
    {
      return BadRequest(new { message = "Not Found Player. Id:" + id.ToString() });
    }

    try
    {
      // delete
      Context.Players.Remove(playerToDelete);
      await Context.SaveChangesAsync();

      return Ok();
    }
    catch (Exception ex)
    {
      var errorMessage = new { message = ex.Message };
      return BadRequest(errorMessage);
    }
  }

  private (bool IsError, object ErrorMessage) Check_JsonData(String datatype, String datavalue)
  {
    // name ??l?? null ?????????????G???[???b?Z?[?W???
    if (string.IsNullOrWhiteSpace(datavalue))
    {
        var errorMessage = new { message = "Empty value for "+ datatype + ": " + datavalue };
        return (true, errorMessage);
    }

    if (datatype == "position" && !new[] { "defender", "midfielder", "forward" }.Contains(datavalue))
    {
        var errorMessage_position = new { message = "Invalid value for " + datatype + ": " + datavalue };
        return (true, errorMessage_position);
    }

    if (datatype == "skill" && !new[] { "defense","attack","speed","strength","stamina" }.Contains(datavalue))
    {
        var errorMessage_skill = new { message = "Invalid value for " + datatype + ": " + datavalue };
        return (true, errorMessage_skill);
    }
    return (false, null);

  }

}