using Godot;
using System.Collections.Generic;
using System.Linq;

public enum RoomsType { Side = 0, Lr = 1, Lrb = 2, Lrt = 3, Lrtb = 4 }

public class Rooms : Node2D
{
	public readonly RoomsType[] BottomOpened = { RoomsType.Lrb, RoomsType.Lrtb };
	public readonly RoomsType[] BottomClosed = { RoomsType.Lr, RoomsType.Lrt };
	public Vector2 RoomSize = Vector2.Zero;
	public Vector2 CellSize = Vector2.Zero;
	private RandomNumberGenerator _rng = new RandomNumberGenerator();

	// Called when the node enters the scene tree for the first time.
	public override void _Ready()
	{
		GD.Print("Hello csharp");
	}

	// _Notification runs when you instance it from code
	public override void _Notification(int what)
	{
		if (what == Node.NotificationInstanced)
		{
			_rng.Randomize();
			var room = GetNode<Node>("Side").GetChild(0) as TileMap;
			if (room != null)
			{
				RoomSize = room.GetUsedRect().Size;
				CellSize = room.CellSize;
			}
		}
		base._Notification(what);
	}

	//  // Called every frame. 'delta' is the elapsed time since the previous frame.
//  public override void _Process(float delta)
//  {
//
//  }

	public List<RoomData> GetRoomData(int type)
	{
		var group = GetChild<Node2D>(type);
		var index = _rng.RandiRange(0, group.GetChildCount() - 1);
		var room = (TileMap) group.GetChild(index);
		var data = new List<RoomData>();
		foreach (Vector2 v in room.GetUsedCells())
		{
			data.Add(new RoomData(v, room.GetCellv(v)));
		}

		return data;
	}
}

public struct RoomData
{
	public Vector2 Offset;
	public int Cell;
	public RoomData(Vector2 offset, int cell)
	{
		Offset = offset;
		Cell = cell;
	}
}
