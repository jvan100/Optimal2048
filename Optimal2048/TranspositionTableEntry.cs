namespace Optimal2048;

public readonly struct TranspositionTableEntry
{
	public int Depth { get; init; }
	public double Heuristic { get; init; }
}