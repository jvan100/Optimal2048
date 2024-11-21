using System.Diagnostics;
using System.Numerics;
using System.Text.Json;
using OpenQA.Selenium;
using OpenQA.Selenium.Chrome;

namespace Optimal2048;

public class WebGame
{
	private readonly IWebDriver _driver;
	private readonly IJavaScriptExecutor _javaScriptExecutor;
	
	public WebGame()
	{
		_driver = new ChromeDriver();
		_javaScriptExecutor = (IJavaScriptExecutor)_driver;
		
		_driver.Navigate().GoToUrl("https://2048game.com");
		
		Setup();
	}
	
	private static void Setup()
	{
		Console.WriteLine("\nSetting up bot...");
		LookupTables.Load();
	}
	
	public void Test()
	{
		Console.Write("Press enter to begin the test...");
		Console.ReadLine();
		Console.WriteLine("\n");
		
		const int testCount = 100;
		
		Dictionary<uint, int> highestTilesCounts = new();

		for (int i = 0; i < testCount; i++)
		{
			Console.WriteLine($"Running test {i + 1}...");
			
			Stopwatch stopwatch = new();
			stopwatch.Start();
			
			Restart();
			
			ulong board = RunGame();
			
			stopwatch.Stop();
			
			TimeSpan timeSpan = stopwatch.Elapsed;
			
			uint highestTile = GetHighestTile();
			highestTilesCounts.TryGetValue(highestTile, out int count);
			highestTilesCounts[highestTile] = count + 1;

			Console.Write($"\r{new string(' ', Console.WindowWidth - 1)}\r");
			Console.WriteLine($"Highest tile: {highestTile}, Score: {GetScore()} ({timeSpan.Minutes} mins {timeSpan.Seconds} secs)\n");
			
			PrintBoard(board);
			ClearGameOverMessage();
		}

		Console.WriteLine($"Test results ({testCount} games):");

		foreach ((uint tile, int count) in highestTilesCounts.OrderBy(pair => pair.Key))
		{
			Console.WriteLine($"{tile}: {count}%");
		}
	}
	
	private ulong RunGame()
	{
		while(true)
		{
			ulong board = GetBoard();
			
			if (IsGameOver())
			{
				return board;
			}
			
			ExpectimaxResult result = ExpectimaxAI.GetNextMove(board);
			MakeMove(result.Move);
		}
	}
	
	public void Play()
	{
		Console.WriteLine("Press enter to start the game...");
		Console.ReadLine();
		
		Console.WriteLine("Playing game...");
		
		Restart();
		
		ulong board = RunGame();
		
		Console.WriteLine("\nGame over!");
		Console.WriteLine($"Highest tile: {GetHighestTile()}, Score: {GetScore()}\n");
		
		PrintBoard(board);
		ClearGameOverMessage();
		
		Console.WriteLine("Press any key to close the window...");
		Console.ReadKey();
	}
	
	private void Restart()
	{
		const string injectionScript = "GameManager._instance = new GameManager(4, KeyboardInputManager, HTMLActuator, LocalStorageManager); GameManager.prototype.isGameTerminated = function() { return this.over; }";
		_javaScriptExecutor.ExecuteScript(injectionScript);
	}
	
	private void ClearGameOverMessage()
	{
		const string injectionScript = "GameManager._instance.actuator.clearMessage();";
		_javaScriptExecutor.ExecuteScript(injectionScript);
	}
	
	private uint[][] GetGrid()
	{
		const string boardScript = "return JSON.stringify(GameManager._instance.grid.cells.map(row => row.map(tile => tile === null ? 0 : tile.value)));";
		
		return JsonSerializer.Deserialize<uint[][]>((string)_javaScriptExecutor.ExecuteScript(boardScript))!;
	}
	
	private ulong GetBoard()
	{
		uint[][] grid = GetGrid();
		
		ulong board = 0;
		
		for (int j = 3; j >= 0; j--)
		{
			for (int i = 3; i >= 0; i--)
			{
				ulong power = (ulong)BitOperations.Log2(grid[i][j]);
				board = (board << 4) | power;
			}
		}
		
		return board;
	}
	
	private uint GetHighestTile()
	{
		return GetGrid().SelectMany(column => column).Max();
	}
	
	private bool IsGameOver()
	{
		const string gameOverScript = "return GameManager._instance.isGameTerminated();";
		return (bool)_javaScriptExecutor.ExecuteScript(gameOverScript);
	}
	
	private void MakeMove(Move move)
	{
		string moveScript = $"GameManager._instance.move({(int)move})";
		_javaScriptExecutor.ExecuteScript(moveScript);
	}
	
	private long GetScore()
	{
		const string scoreScript = "return GameManager._instance.score";
		return (long)_javaScriptExecutor.ExecuteScript(scoreScript);
	}
	
	private static void PrintBoard(ulong board)
	{
		Console.WriteLine($"Board: {board}");
		Console.WriteLine(new string('-', 37));
		
		for (int i = 0; i < 4; i++)
		{
			for (int j = 0; j < 4; j++)
			{
				int power = (int)(board & 0xF);
				Console.Write("| {0,6} ", power == 0 ? "" : 1 << power);
				
				board >>= 4;
			}

			Console.WriteLine("|");
			Console.WriteLine(new string('-', 37));
		}

		Console.WriteLine();
	}
}