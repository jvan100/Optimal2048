using System.Text;

namespace Optimal2048.Util;

internal sealed class ProgressBar : IDisposable, IProgress<double>
{
	private const string ANIMATION = @"|/-\";
	private const int TOTAL_BLOCK_COUNT = 40;
	
	private readonly TimeSpan _animationInterval = TimeSpan.FromSeconds(1.0 / 8);
	private readonly Timer _timer;

	private int _animationIndex;
	private bool _finished;
	private double _progress;
	private string _text = string.Empty;
	
	internal ProgressBar()
	{
		_timer = new Timer(TimerHandler);
		ResetTimer();
	}
	
	public void Dispose()
	{
		lock (_timer)
		{
			_finished = true;
			
			UpdateText("\u2713");
			Console.WriteLine();
			
			_timer.Dispose();
		}
	}
	
	public void Report(double value)
	{
		Interlocked.Exchange(ref _progress, Math.Max(0, Math.Min(1, value)));
	}
	
	private void ResetTimer()
	{
		_timer.Change(_animationInterval, TimeSpan.FromMilliseconds(-1));
	}

	private void TimerHandler(object? state)
	{
		lock (_timer)
		{
			if (_finished)
			{
				return;
			}

			int progressBlockCount = (int)(_progress * TOTAL_BLOCK_COUNT);
			int percent = (int)(_progress * 100);

			string text = $"[{new string('#', progressBlockCount)}{new string('-', TOTAL_BLOCK_COUNT - progressBlockCount)}] {percent}% {ANIMATION[_animationIndex++ % 4]}";
			UpdateText(text);

			ResetTimer();
		}
	}

	private void UpdateText(string text)
	{
		int commonPrefixLength = 0;
		int maxCommonPrefixLength = Math.Min(_text.Length, text.Length);

		while (commonPrefixLength < maxCommonPrefixLength && text[commonPrefixLength] == _text[commonPrefixLength])
		{
			commonPrefixLength++;
		}

		StringBuilder progressBarBuilder = new();
		progressBarBuilder.Append('\b', _text.Length - commonPrefixLength);
		progressBarBuilder.Append(text[commonPrefixLength..]);

		int overlappingCount = _text.Length - text.Length;

		if (overlappingCount > 0)
		{
			progressBarBuilder.Append(' ', overlappingCount);
			progressBarBuilder.Append('\b', overlappingCount);
		}

		Console.Write(progressBarBuilder.ToString());
		_text = text;
	}
}