using Optimal2048.Util;

namespace Optimal2048;

public static class LookupTables
{
	private const int NO_ENTRIES = 65536;
	private const double SCORE_LOST_PENALTY = 200000;
	private const double SCORE_MONOTONICITY_POWER = 4;
	private const double SCORE_MONOTONICITY_WEIGHT = 47;
	private const double SCORE_SUM_POWER = 3.5;
	private const double SCORE_SUM_WEIGHT = 11;
	private const double SCORE_MERGES_WEIGHT = 700;
	private const double SCORE_EMPTY_WEIGHT = 270;
	
	private const string DIRECTORY = "lookup_tables";
	private const string LEFT_MOVE_TABLE_FILE_PATH = $"{DIRECTORY}/left_move_table.bin";
	private const string RIGHT_MOVE_TABLE_FILE_PATH = $"{DIRECTORY}/right_move_table.bin";
	private const string UP_MOVE_TABLE_FILE_PATH = $"{DIRECTORY}/up_move_table.bin";
	private const string DOWN_MOVE_TABLE_FILE_PATH = $"{DIRECTORY}/down_move_table.bin";
	private const string SCORE_TABLE_FILE_PATH = $"{DIRECTORY}/score_table.bin";
	private const string HEURISTIC_TABLE_FILE_PATH = $"{DIRECTORY}/heuristic_table.bin";
	
	public static ulong[] LeftMove { get; private set; }
	public static ulong[] RightMove { get; private set; }
	public static ulong[] UpMove { get; private set; }
	public static ulong[] DownMove { get; private set; }
	public static int[] Score { get; private set; }
	public static double[] Heuristic { get; private set; }
	
	public static void Load()
	{
		using (ProgressBar progressBar = new ProgressBar())
		{
			if (File.Exists(LEFT_MOVE_TABLE_FILE_PATH) && 
			    File.Exists(RIGHT_MOVE_TABLE_FILE_PATH) && 
			    File.Exists(UP_MOVE_TABLE_FILE_PATH) && 
			    File.Exists(DOWN_MOVE_TABLE_FILE_PATH) &&
			    File.Exists(SCORE_TABLE_FILE_PATH) &&
			    File.Exists(HEURISTIC_TABLE_FILE_PATH))
			{
				Console.Write("Loading lookup tables... ");
			
				LeftMove = LoadTable<ulong>(LEFT_MOVE_TABLE_FILE_PATH);
				RightMove = LoadTable<ulong>(RIGHT_MOVE_TABLE_FILE_PATH);
				UpMove = LoadTable<ulong>(UP_MOVE_TABLE_FILE_PATH);
				DownMove = LoadTable<ulong>(DOWN_MOVE_TABLE_FILE_PATH);
			
				Score = LoadTable<int>(SCORE_TABLE_FILE_PATH);
			
				Heuristic = LoadTable<double>(HEURISTIC_TABLE_FILE_PATH);
			} 
			else
			{
				Generate(progressBar);
			}
		}

		Console.WriteLine();
	}
	
	private static void Generate(ProgressBar progressBar)
	{
		LeftMove = new ulong[NO_ENTRIES];
		RightMove = new ulong[NO_ENTRIES];
		UpMove = new ulong[NO_ENTRIES];
		DownMove = new ulong[NO_ENTRIES];
		
		Score = new int[NO_ENTRIES];
		
		Heuristic = new double[NO_ENTRIES];
		
		Console.Write("Generating move tables... ");
		
		for (ulong row = 0; row < NO_ENTRIES; row++)
		{
			ulong[] rowTiles = GetTilesFromRow(row);
				
			ulong result = ComputeLeftRowMove(rowTiles);
			LeftMove[row] = row ^ result;
				
			ulong reversedRow = ReverseRow(row);
			ulong reversedResult = ReverseRow(result);
			RightMove[reversedRow] = reversedRow ^ reversedResult;
				
			UpMove[row] = UnpackColumn(row) ^ UnpackColumn(result);
			DownMove[reversedRow] = UnpackColumn(reversedRow) ^ UnpackColumn(reversedResult);
				
			Score[row] = CalculateScore(rowTiles);
				
			Heuristic[row] = CalculateHeuristicScore(rowTiles);
				
			progressBar.Report((double)row / (NO_ENTRIES - 1));
		}
		
		WriteTables();
	}
	
	private static ulong GetRowFromTiles(ulong[] tiles) => tiles[0] | tiles[1] << 4 | tiles[2] << 8 | tiles[3] << 12;
	
	private static ulong[] GetTilesFromRow(ulong row) => new[] { row & 0xF, (row >> 4) & 0xF, (row >> 8) & 0xF, (row >> 12) & 0xF };
	
	private static ulong UnpackColumn(ulong row) => (row | (row << 12) | (row << 24) | (row << 36)) & Constants.COLUMN_MASK;
	
	private static ulong ReverseRow(ulong row) => (row >> 12 | ((row >> 4) & 0x00F0) | ((row << 4) & 0x0F00) | row << 12) & Constants.ROW_MASK;
	
	private static ulong ComputeLeftRowMove(ulong[] rowTiles)
	{
		MoveTilesLeft(rowTiles);
		MergeTilesLeft(rowTiles);
		MoveTilesLeft(rowTiles);
		
		return GetRowFromTiles(rowTiles);
	}
	
	private static void MoveTilesLeft(ulong[] rowTiles)
	{
		for (int i = 1; i < 4; i++)
		{
			ulong value = rowTiles[i];
			
			if (value != 0)
			{
				int j = i;
				
				while (j > 0 && rowTiles[j - 1] == 0)
				{
					j--;
				}
				
				if (i != j)
				{
					rowTiles[i] = 0;
					rowTiles[j] = value;
				}
			}
		}
	}
	
	private static void MergeTilesLeft(ulong[] rowTiles)
	{
		for (int i = 1; i < 4; i++)
		{
			ulong value = rowTiles[i];
			int j = i - 1;
			
			if (value != 0 && rowTiles[j] == value)
			{
				rowTiles[i] = 0;
				rowTiles[j] = value + 1;
			}
		}
	}
	
	private static int CalculateScore(ulong[] rowTiles)
	{
		int score = 0;

		for (int i = 0; i < 4; i++)
		{
			int power = (int)rowTiles[i];
			
			if (power > 1)
			{
				score += (power - 1) * (1 << power);
			}
		}
		
		return score;
	}
	
	private static double CalculateHeuristicScore(ulong[] tiles)
	{
		double sum = 0;
		int empty = 0;
		int merges = 0;
		
		int previous = 0;
		int counter = 0;

		for (int i = 0; i < 4; i++)
		{
			int power = (int)tiles[i];
			sum += Math.Pow(power, SCORE_SUM_POWER);
			
			if (power == 0)
			{
				empty++;
			}
			else
			{
				if (previous == power)
				{
					counter++;
				}
				else if (counter > 0)
				{
					merges += counter + 1;
					counter = 0;
				}
				
				previous = power;
			}
		}
		
		if (counter > 0)
		{
			merges += counter + 1;
		}
		
		double monotonicityLeft = 0;
		double monotonicityRight = 0;
		
		for (int i = 1; i < 4; i++)
		{
			if (tiles[i - 1] > tiles[i])
			{
				monotonicityLeft += Math.Pow(tiles[i - 1], SCORE_MONOTONICITY_POWER) - Math.Pow(tiles[i], SCORE_MONOTONICITY_POWER);
			}
			else
			{
				monotonicityRight += Math.Pow(tiles[i], SCORE_MONOTONICITY_POWER) - Math.Pow(tiles[i - 1], SCORE_MONOTONICITY_POWER);
			}
		}
		
		return SCORE_LOST_PENALTY + SCORE_EMPTY_WEIGHT * empty + SCORE_MERGES_WEIGHT * merges - SCORE_MONOTONICITY_WEIGHT * Math.Min(monotonicityLeft, monotonicityRight) - SCORE_SUM_WEIGHT * sum;
	}
	
	private static void WriteTables()
	{
		EnsureDirectoryExists(DIRECTORY);
		
		WriteTable(LEFT_MOVE_TABLE_FILE_PATH, LeftMove);
		WriteTable(RIGHT_MOVE_TABLE_FILE_PATH, RightMove);
		WriteTable(UP_MOVE_TABLE_FILE_PATH, UpMove);
		WriteTable(DOWN_MOVE_TABLE_FILE_PATH, DownMove);
		
		WriteTable(SCORE_TABLE_FILE_PATH, Score);
		
		WriteTable(HEURISTIC_TABLE_FILE_PATH, Heuristic);
	}
	
	private static void EnsureDirectoryExists(string directoryPath)
	{
		if (!Directory.Exists(directoryPath))
		{
			Directory.CreateDirectory(directoryPath);
		}
	}
	
	private static T[] LoadTable<T>(string filePath)
	{
		using FileStream fs = new FileStream(filePath, FileMode.Open, FileAccess.Read);
		using BinaryReader br = new BinaryReader(fs);
		
		T[] table = new T[NO_ENTRIES];

		for (int i = 0; i < NO_ENTRIES; i++)
		{
			table[i] = (T)Convert.ChangeType(br.ReadDouble(), typeof(T));
		}
		
		return table;
	}
	
	private static void WriteTable<T>(string filePath, T[] table)
	{
		using FileStream fs = new FileStream(filePath, FileMode.Create, FileAccess.Write);
		using BinaryWriter bw = new BinaryWriter(fs);

		foreach (T value in table)
		{
			bw.Write(Convert.ToDouble(value));
		}
	}
	
}