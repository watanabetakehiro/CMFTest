// /////////////////////////////////////////////////////////////////////////////
// PLEASE DO NOT RENAME OR REMOVE ANY OF THE CODE BELOW. 
// YOU CAN ADD YOUR CODE TO THIS FILE TO EXTEND THE FEATURES TO USE THEM IN YOUR WORK.
// /////////////////////////////////////////////////////////////////////////////

using System.ComponentModel.DataAnnotations;

namespace WebApi.Entities;

public class TeamRequirement
{
    public string Position { get; set; }
    public string MainSkill { get; set; }
    public int NumberOfPlayers { get; set; }
}
