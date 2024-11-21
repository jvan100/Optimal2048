using System.ComponentModel;
using System.Diagnostics;

namespace Optimal2048;

public static class ExpectimaxAI
{
	private const double CUMULATIVE_PROBABILITY_THRESHOLD = 0.0001;
	private const int CACHE_DEPTH_LIMIT = 15;
	
	private static readonly Move[] _allMoves = { Move.Up, Move.Right, Move.Down, Move.Left };
	
	public static ExpectimaxResult GetNextMove(ulong board)
	{
		int depthLimit = GetDepthLimit(board);
		
		ExpectimaxResult bestResult = new ExpectimaxResult
		{
			Score = double.NegativeInfinity
		};
		
		object lockObj = new object();
		
		Stopwatch stopwatch = new Stopwatch();
		stopwatch.Start();
		
		Parallel.ForEach(_allMoves, move =>
		{
			ExpectimaxResult result = Search(board, move, depthLimit);
		
			lock (lockObj)
			{
				if (result.Score > bestResult.Score)
				{
					bestResult = result;
				}
			}
		});
		
		stopwatch.Stop();
		
		TimeSpan timeSpan = stopwatch.Elapsed;
		
		string output = $"Board: {board, -20} Best Move: {bestResult.Move, -6} Heuristic: {Math.Floor(bestResult.Score), -9} Max Depth Searched: {bestResult.MaxDepthSearched, -2} ({timeSpan.TotalSeconds:0.000} secs)";
		
		Console.Write($"\r{new string(' ', Console.WindowWidth - 1)}\r");
		Console.Write(output);
		
		return bestResult;
	}
	
	private static ExpectimaxResult Search(ulong board, Move move, int depthLimit)
	{
		ExpectimaxResult result = new ExpectimaxResult
		{
			Move = move
		};
		
		ulong newBoard = MakeMove(board, move);
			
		if (newBoard == board)
		{
			result.Score = 0;
		} 
		else
		{
			EvaluationState state = new EvaluationState(depthLimit);
			result.Score = EvaluateRandomTileNode(newBoard, ref state, 1);
			result.MaxDepthSearched = state.MaxDepth;
		}
		
		return result;
	}
	
	private static double EvaluateRandomTileNode(ulong board, ref EvaluationState state, double cumulativeProbability)
	{
		if (cumulativeProbability < CUMULATIVE_PROBABILITY_THRESHOLD || state.CurrentDepth >= state.DepthLimit)
		{
			state.MaxDepth = Math.Max(state.CurrentDepth, state.MaxDepth);
			return EvaluateBoard(board);
		}

		if (state.CurrentDepth < CACHE_DEPTH_LIMIT && state.TranspositionTable.TryGetValue(board, out TranspositionTableEntry entry) && entry.Depth <= state.CurrentDepth)
		{
			return entry.Heuristic;
		}
		
		int emptyTileCount = CountEmptyTiles(board);
		cumulativeProbability /= emptyTileCount;
		
		double score = 0;
		ulong tmp = board;
		ulong randomTile = 1;
		
		for (int i = 0; i < 16; i++)
		{
			if ((tmp & 0xF) == 0)
			{
				score += EvaluateMoveNode(board | randomTile, ref state, cumulativeProbability * Constants.TWO_SPAWN_PROBABILITY) * Constants.TWO_SPAWN_PROBABILITY;
				score += EvaluateMoveNode(board | (randomTile << 1), ref state, cumulativeProbability * Constants.FOUR_SPAWN_PROBABILITY) * Constants.FOUR_SPAWN_PROBABILITY;
			}
			
			tmp >>= 4;
			randomTile <<= 4;
		}
		
		score /= emptyTileCount;
		
		if (state.CurrentDepth < CACHE_DEPTH_LIMIT)
		{
			state.TranspositionTable[board] = new TranspositionTableEntry
			{
				Depth = state.CurrentDepth,
				Heuristic = score
			};
		}
		
		return score;
	}
	
	private static double EvaluateMoveNode(ulong board, ref EvaluationState state, double cumulativeProbability)
	{
		state.CurrentDepth++;
		
		double bestScore = 0;

		foreach (Move move in _allMoves)
		{
			ulong newBoard = MakeMove(board, move);
			
			if (newBoard != board)
			{
				bestScore = Math.Max(bestScore, EvaluateRandomTileNode(newBoard, ref state, cumulativeProbability));
			}
		}
		
		state.CurrentDepth--;
		
		return bestScore;
	}
	
	private static double EvaluateBoard(ulong board)
	{
		double score = LookupTables.Heuristic[board & Constants.ROW_MASK] + 
		               LookupTables.Heuristic[(board >> 16) & Constants.ROW_MASK] + 
		               LookupTables.Heuristic[(board >> 32) & Constants.ROW_MASK] + 
		               LookupTables.Heuristic[(board >> 48) & Constants.ROW_MASK];
		
		ulong transpose = Transpose(board);
		
		double transposeScore = LookupTables.Heuristic[transpose & Constants.ROW_MASK] + 
		                        LookupTables.Heuristic[(transpose >> 16) & Constants.ROW_MASK] + 
		                        LookupTables.Heuristic[(transpose >> 32) & Constants.ROW_MASK] + 
		                        LookupTables.Heuristic[(transpose >> 48) & Constants.ROW_MASK];
		
		return score + transposeScore;
	}
	
	private static ulong MakeMove(ulong board, Move move) => move switch
	{
		Move.Up => MakeUpMove(board),
		Move.Down => MakeDownMove(board),
		Move.Left => MakeLeftMove(board),
		Move.Right => MakeRightMove(board),
		_ => throw new InvalidEnumArgumentException(nameof(move), (int)move, typeof(Move))
	};
	
	private static ulong MakeUpMove(ulong board)
	{
		ulong transpose = Transpose(board);
		board ^= LookupTables.UpMove[transpose & Constants.ROW_MASK];
		board ^= LookupTables.UpMove[(transpose >> 16) & Constants.ROW_MASK] << 4;
		board ^= LookupTables.UpMove[(transpose >> 32) & Constants.ROW_MASK] << 8;
		return board ^ LookupTables.UpMove[(transpose >> 48) & Constants.ROW_MASK] << 12;
	}
	
	private static ulong MakeDownMove(ulong board)
	{
		ulong transpose = Transpose(board);
		board ^= LookupTables.DownMove[transpose & Constants.ROW_MASK];
		board ^= LookupTables.DownMove[(transpose >> 16) & Constants.ROW_MASK] << 4;
		board ^= LookupTables.DownMove[(transpose >> 32) & Constants.ROW_MASK] << 8;
		return board ^ LookupTables.DownMove[(transpose >> 48) & Constants.ROW_MASK] << 12;
	}
	
	private static ulong MakeLeftMove(ulong board)
	{
		board ^= LookupTables.LeftMove[board & Constants.ROW_MASK];
		board ^= LookupTables.LeftMove[(board >> 16) & Constants.ROW_MASK] << 16;
		board ^= LookupTables.LeftMove[(board >> 32) & Constants.ROW_MASK] << 32;
		return board ^ LookupTables.LeftMove[(board >> 48) & Constants.ROW_MASK] << 48;
	}
	
	private static ulong MakeRightMove(ulong board)
	{
		board ^= LookupTables.RightMove[board & Constants.ROW_MASK];
		board ^= LookupTables.RightMove[(board >> 16) & Constants.ROW_MASK] << 16;
		board ^= LookupTables.RightMove[(board >> 32) & Constants.ROW_MASK] << 32;
		return board ^ LookupTables.RightMove[(board >> 48) & Constants.ROW_MASK] << 48;
	}
	
	private static ulong Transpose(ulong board)
	{
		ulong tmp = (board & 0xF0F0_0F0F_F0F0_0F0F) | 
		            ((board & 0x0000_F0F0_0000_F0F0) << 12) | 
		            ((board & 0x0F0F_0000_0F0F_0000) >> 12);
		
		return (tmp & 0xFF00_FF00_00FF_00FF) | 
		       ((tmp & 0x0000_000_FF00_FF00) << 24) | 
		       ((tmp & 0x00FF_00FF_0000_0000) >> 24);
	}
	
	private static int CountEmptyTiles(ulong board)
	{
		board |= (board >> 2) & 0x3333_3333_3333_3333;
		board |= board >> 1;
		board = ~board & 0x1111_1111_1111_1111;
			
		board += board >> 32;
		board += board >> 16;
		board += board >> 8;
		board += board >> 4;
			
		return (int)(board & 0xF);
	}
	
	private static int GetDepthLimit(ulong board)
	{
		uint bitset = 0;
		
		while (board != 0)
		{
			bitset |= 1U << (int)(board & 0xf);
			board >>= 4;
		}
		
		bitset >>= 1;
		
		int distinctTiles = 0;
		
		while(bitset != 0)
		{
			bitset &= bitset - 1;
			distinctTiles++;
		}
		
		return Math.Max(3, distinctTiles - 2);
	}
	
}