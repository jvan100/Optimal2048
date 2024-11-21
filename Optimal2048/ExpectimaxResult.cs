namespace Optimal2048;

public struct ExpectimaxResult
{
	public Move Move { get; init; }
	public double Score { get; set; }
	public int MaxDepthSearched { get; set; }
}