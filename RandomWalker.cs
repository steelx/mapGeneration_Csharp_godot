using System;
using System.Collections.Generic;
using System.Linq;
using Godot;

public class RandomWalker : Node2D
{
    [Export] public PackedScene Rooms = ResourceLoader.Load<PackedScene>("res://Rooms.tscn");
    [Export] public Vector2 GridSize = new Vector2(8, 6);
    
    // When we finished calculating the valid path, we emit
    [Signal] public delegate void PathCompleted();

    private Rooms _rooms;
    private RandomNumberGenerator _rng = new RandomNumberGenerator();
    private State _state = new State();
    private double _horizontalChance = 0.0;
    private static readonly Vector2[] step = {
        Vector2.Left, Vector2.Left, Vector2.Right, Vector2.Right, Vector2.Down
    };

    public Camera2D camera;
    public Timer timer;
    public TileMap level;
    
    // Called when the node enters the scene tree for the first time.
    public override void _Ready()
    {
        timer = GetNode<Timer>("Timer");
        camera = GetNode<Camera2D>("Camera2D");
        level = GetNode<TileMap>("Level");
        _rng.Randomize();
        _rooms = (Rooms)Rooms.Instance();
        // find occurrences of Vector2.Down
        var count = Array.FindAll(step, vector2 => vector2.Equals(Vector2.Down)).Length;
        _horizontalChance = 1.0 - (double)count / step.Length;
        _setup_camera();
        _generate_level();
    }

    private void _generate_level()
    {
        _reset();
        _update_start_position();
        while (_state.Offset.y < GridSize.y)
        {
            _update_room_type();
            _update_next_position();
            _update_down_counter();
        }

        _place_walls();
        _place_side_rooms();
        _place_path_rooms();
    }

    private async void _place_path_rooms()
    {
        foreach (var path in _state.Path)
        {
            await ToSignal(timer, "timeout");
            GD.Print("timeout works");
            _copy_room(path.Offset, (int)path.RoomsType);
        }
        EmitSignal(nameof(PathCompleted));
        GD.Print("_place_path_rooms END");
    }

    private async void _place_side_rooms()
    {
        await ToSignal(this, nameof(PathCompleted));
        GD.Print("should I stay of should I go ! PathCompleted");
        var typesArr = Enum.GetValues(typeof(RoomsType));
        var roomsMaxIndex = typesArr.Length - 1;
        foreach (var emptyCell in _state.EmptyCells)
        {
            var index = _rng.RandiRange(0, roomsMaxIndex);
            GD.Print("calling _copy_room");
            _copy_room(emptyCell.Key, index);
        }
    }

    private void _copy_room(Vector2 offset, int type)
    {
        var mapOffset = _grid_to_map(offset);
        var data = _rooms.GetRoomData(type);
        foreach (var roomData in data)
        {
            level.SetCellv(mapOffset + roomData.Offset, roomData.Cell);
        }
    }

    private void _place_walls(int type = 0)
    {
        var cellGridSize = _grid_to_map(GridSize);
        

        foreach (var x in Enumerable.Range(-1, (int)cellGridSize.x))
        {
            foreach (var y in Enumerable.Range(-1, (int)cellGridSize.y+1))
            {
                level.SetCell(x, y, type);
            }
        }
        
        foreach (var x in Enumerable.Range(0, (int)cellGridSize.x+1))
        {
            foreach (var y in Enumerable.Range(-1, (int)cellGridSize.y))
            {
                level.SetCell(x, y, type);
            }
        }
    }

    private void _update_down_counter()
    {
        _state.DownCounter = _state.Delta.IsEqualApprox(Vector2.Down) ? _state.DownCounter + 1 : 0;
    }

    private void _update_next_position()
    {
        _state.RandomIndex = _state.RandomIndex < 0 ? _rng.RandiRange(0, step.Length-1) : _state.RandomIndex;
        _state.Delta = step[_state.RandomIndex];

        var horizontalChance = _rng.Randf();
        if (_state.Delta.IsEqualApprox(Vector2.Left))
        {
            _state.RandomIndex = _state.Offset.x > 1 && horizontalChance < _horizontalChance ? 0 : 4;
        } else if (_state.Delta.IsEqualApprox(Vector2.Right))
        {
            _state.RandomIndex = _state.Offset.x < GridSize.x - 1 && horizontalChance < _horizontalChance ? 2 : 4;
        }
        else
        {
            //we check we're not at the left or right edges of the grid.
            //If we’re not, we update _state.random_index to a random value because any position is valid from here.
            if (_state.Offset.x > 0 && _state.Offset.x < GridSize.x-1)
            {
                _state.RandomIndex = _rng.RandiRange(0, 4);
            }
            else if(_state.Offset.x == 0)
            {
                // we've hit left boundary of the grid
                _state.RandomIndex = horizontalChance < _horizontalChance ? 2 : 4;
            } else if (Math.Abs(_state.Offset.x - (GridSize.x - 1)) < 0.001f)
            {
                //we're at the right boundary of the grid
                _state.RandomIndex = horizontalChance < _horizontalChance ? 0 : 4;
            }
        }

        _state.Delta = step[_state.RandomIndex];
        _state.Offset += _state.Delta;
    }

    private void _update_room_type()
    {
        int index;
        RoomsType roomsType;
        // is not Empty
        if (_state.Path.Any())
        {
            var last = _state.Path.Last();
            if (_rooms.BottomClosed.Contains(last.RoomsType) && _state.Delta.IsEqualApprox(Vector2.Down))
            {
                index = _rng.RandiRange(0, _rooms.BottomOpened.Length - 1);
                roomsType = _state.DownCounter < 2 ? _rooms.BottomClosed[index] : RoomsType.Lrtb;
                // _state.Path[_state.Path.Count-1] = new PathObj();
                _state.Path[_state.Path.Count - 1].RoomsType = roomsType;
            }
        }
        
        var typesArr = Enum.GetValues(typeof(RoomsType));
        index = _rng.RandiRange(0, typesArr.Length - 1);
        roomsType = _state.Delta.Equals(Vector2.Down) ? RoomsType.Lrt : (RoomsType)typesArr.GetValue(index);
        _state.EmptyCells.Remove(_state.Offset);
        _state.Path.Add(new PathObj(_state.Offset, roomsType));
        GD.Print(_state.Path.Count);
    }

    private void _update_start_position()
    {
        var x = _rng.RandiRange(0, (int)GridSize.x - 1);
        _state.Offset = new Vector2(x, 0);
    }

    private void _reset()
    {
        _state = new State();

        foreach (var x in Enumerable.Range(0, (int)GridSize.x))
        {
            foreach (var y in Enumerable.Range(0, (int) GridSize.y))
            {
                _state.EmptyCells.Add(new Vector2(x, y), 0);
            }
        }
    }

    private void _setup_camera()
    {
        var worldSize = _grid_to_world(GridSize);
        camera.Position = worldSize / 2;

        var ratio = worldSize / OS.WindowSize;
        var zoomMax = Math.Max(ratio.x, ratio.y) + 1;
        camera.Zoom = new Vector2(zoomMax, zoomMax);
    }

    private Vector2 _grid_to_map(Vector2 gridSize)
    {
        return _rooms.RoomSize * gridSize;
    }
    
    private Vector2 _grid_to_world(Vector2 gridSize)
    {
        return _rooms.CellSize * _rooms.RoomSize * gridSize;
    }

    //  // Called every frame. 'delta' is the elapsed time since the previous frame.
//  public override void _Process(float delta)
//  {
//      
//  }
}

public class State
{
    // walker's current position on the grid
    public Vector2 Offset { set; get; }
    // Direction to increment the offset key above
    public Vector2 Delta { set; get; }
    //Random index to pick a direction from the STEP constant array from last lesson
    public int RandomIndex { set; get; }
    // number of times the walker moved down without interruption
    public int DownCounter { set; get; }
    // The level's unobstructed path
    public List<PathObj> Path { set; get; }
    // Coordinates of the cells we haven't populated yet
    public Dictionary<Vector2, int> EmptyCells;

    public State()
    {
        Offset = Vector2.Zero;
        Delta = Vector2.Zero;
        RandomIndex = -1;
        DownCounter = 0;
        Path = new List<PathObj>();
        EmptyCells = new Dictionary<Vector2, int>();
    }
}

public class PathObj
{
    public RoomsType RoomsType;
    public Vector2 Offset;

    public PathObj(Vector2 offset, RoomsType roomsType)
    {
        RoomsType = roomsType;
        Offset = offset;
    }
}