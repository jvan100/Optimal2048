namespace Optimal2048;

public struct EvaluationState
{
	public Dictionary<ulong, TranspositionTableEntry> TranspositionTable { get; }
	public int MaxDepth { get; set; }
	public byte CurrentDepth { get; set; }
	public int DepthLimit { get; }
	
	public EvaluationState(int depthLimit)
	{
		TranspositionTable = new();
		MaxDepth = 0;
		CurrentDepth = 0;
		DepthLimit = depthLimit;
	}
}