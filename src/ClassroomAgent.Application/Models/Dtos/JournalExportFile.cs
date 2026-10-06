namespace ClassroomAgent.Application.Models.Dtos;

/// <summary>US-028 entity model §3.2: the bytes and the file name of an exported journal.</summary>
public sealed record JournalExportFile(byte[] Content, string FileName);
