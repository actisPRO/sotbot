using System;

namespace Bot_NetCore.Models;

public record MemberJoin
{
    public string MemberId { get; set; }
    public string Username { get; set; } = String.Empty;
    public DateTime JoinDate { get; set; }
    public string Invite { get; set; }
};