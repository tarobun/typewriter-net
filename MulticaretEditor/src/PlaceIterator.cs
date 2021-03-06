using System;

namespace MulticaretEditor
{
	public class PlaceIterator
	{
		private readonly LineBlock[] blocks;
		private readonly int blocksCount;
		private readonly int charsCount;
		private int blockI;
		private int blockILine;
		private int iChar;

		public PlaceIterator(LineBlock[] blocks, int blocksCount, int charsCount, int blockI, int blockILine, int iChar, int position)
		{
			this.blocks = blocks;
			this.blocksCount = blocksCount;
			this.charsCount = charsCount;
			this.blockI = blockI;
			this.blockILine = blockILine;
			this.iChar = iChar;
			this.position = position;
			if (iChar == blocks[blockI].array[blockILine].charsCount)
			{
				rightChar = '\0';
			}
			else
			{
				rightChar = blocks[blockI].array[blockILine].chars[iChar].c;
			}
		}

		public Place Place { get { return new Place(iChar, blocks[blockI].offset + blockILine); } }

		private char rightChar;
		public char RightChar { get { return rightChar; } }

		private char? leftChar = null;
		public char LeftChar
		{
			get
			{
				if (leftChar == null)
				{
					leftChar = '\0';
					int blockI = this.blockI;
					int blockILine = this.blockILine;
					int iChar = this.iChar;
					if (position > 0)
					{
						LineBlock block = blocks[blockI];
						Line line = block.array[blockILine];
						if (iChar == 0)
						{
							if (blockILine == 0)
							{
								blockI--;
								block = blocks[blockI];
								blockILine = block.count - 1;
							}
							else
							{
								blockILine--;
							}
							line = block.array[blockILine];
							iChar = line.charsCount - 1;
						}
						else
						{
							iChar--;
						}
						leftChar = line.chars[iChar].c;
					}
				}
				return leftChar.Value;
			}
		}
		
		public int GetLeftCharsCount(char c)
		{
			int count = 0;
			if (position > 0)
			{
				LineBlock block = blocks[blockI];
				Line line = block.array[blockILine];
				for (int iChar = this.iChar; iChar-- > 0;)
				{
					if (line.chars[iChar].c != c)
					{
						break;
					}
					++count;
				}
			}
			return count;
		}

		private int position;
		public int Position { get { return position; } }

		public bool MoveLeft(out char c)
		{
			bool result = MoveLeft();
			c = RightChar;
			return result;
		}

		public bool MoveLeft()
		{
			leftChar = null;
			bool result = position > 0;
			rightChar = '\0';
			if (result)
			{
				LineBlock block = blocks[blockI];
				Line line = block.array[blockILine];
				if (iChar == 0)
				{
					if (blockILine == 0)
					{
						blockI--;
						block = blocks[blockI];
						blockILine = block.count - 1;
					}
					else
					{
						blockILine--;
					}
					line = block.array[blockILine];
					iChar = line.charsCount - 1;
				}
				else
				{
					iChar--;
				}
				rightChar = line.chars[iChar].c;
				position--;
			}
			return result;
		}

		public bool MoveRight(out char c)
		{
			c = RightChar;
			return MoveRight();
		}

		public bool MoveRight()
		{
			bool result = position < charsCount;
			if (result)
			{
				leftChar = rightChar;
				rightChar = '\0';
				LineBlock block = blocks[blockI];
				Line line = block.array[blockILine];
				if (iChar == line.charsCount - 1)
				{
					if (blockILine == block.count - 1)
					{
						if (blockI >= blocksCount - 1)
						{
							rightChar = '\0';
							iChar++;
							position++;
							return true;
						}
						blockI++;
						block = blocks[blockI];
						blockILine = 0;
					}
					else
					{
						blockILine++;
					}
					line = block.array[blockILine];
					iChar = 0;
					rightChar = line.charsCount > 0 ? line.chars[iChar].c : '\0';
				}
				else
				{
					iChar++;
					rightChar = line.chars[iChar].c;
				}
				position++;
			}
			else
			{
				rightChar = '\0';
			}
			return result;
		}

		public bool MoveRightWithRN()
		{
			char c;
			bool moved = MoveRight(out c);
			if (moved && c == '\r' && RightChar == '\n')
				MoveRight();
			return moved;
		}

		public bool MoveLeftWithRN()
		{
			char c;
			bool moved = MoveLeft(out c);
			if (moved && c == '\n' && LeftChar == '\r')
				MoveLeft();
			return moved;
		}
	}
}
