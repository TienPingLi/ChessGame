using System.Drawing.Drawing2D;
using System.Media;

namespace AnimatedChessGame;

public partial class MainForm : Form
{
    private enum Side { White, Black }
    private enum Kind { King, Queen, Rook, Bishop, Knight, Pawn }
    private enum GameEndReason { None, Checkmate, Stalemate, Timeout }

    private readonly record struct Piece(Side Side, Kind Kind);
    private readonly record struct Move(int FromRow, int FromCol, int ToRow, int ToCol);
    private readonly record struct SearchUndo(Move Move, Piece MovingPiece, Piece? CapturedPiece);

    private sealed class MateParticle
    {
        public float X { get; set; }
        public float Y { get; set; }
        public float Vx { get; set; }
        public float Vy { get; set; }
        public float Size { get; set; }
        public float Life { get; set; }
        public float MaxLife { get; set; }
    }

    private sealed class HistoryEntry
    {
        public required Move Move { get; init; }
        public required Piece MovingPiece { get; init; }
        public required Piece? CapturedPiece { get; init; }
        public required Side PreviousTurn { get; init; }
        public required Move? PreviousLastMove { get; init; }
        public required int PreviousWhiteSeconds { get; init; }
        public required int PreviousBlackSeconds { get; init; }
        public required bool PreviousGameOver { get; init; }
        public string Notation { get; set; } = string.Empty;
    }

    private sealed class GameStateInfo
    {
        public bool InCheck { get; init; }
        public bool HasMove { get; init; }
        public GameEndReason EndReason { get; init; }
    }

    private const int BoardSize = 8;
    private const int SidePanelMinWidth = 270;
    private const int Margin = 24;

    private readonly Piece?[,] board = new Piece?[BoardSize, BoardSize];
    private readonly Dictionary<string, Image> pieceImages = new();
    private readonly List<HistoryEntry> history = new();
    private readonly List<Piece> capturedByWhite = new();
    private readonly List<Piece> capturedByBlack = new();
    private readonly Random rng = new();

    private readonly System.Windows.Forms.Timer animationTimer = new();
    private readonly System.Windows.Forms.Timer clockTimer = new();
    private readonly System.Windows.Forms.Timer botTimer = new();
    private readonly System.Windows.Forms.Timer checkmateTimer = new();

    private int tile = 78;
    private int boardLeft = 28;
    private int boardTop = 28;
    private int boardPixels = 78 * 8;

    private Side currentTurn = Side.White;
    private Point? selected;
    private List<Move> legalMoves = new();
    private Move? lastMove;
    private Move? hintMove;
    private bool gameOver;
    private bool paused;
    private bool botThinking;
    private bool checkmateAnimating;
    private int checkmateFrame;
    private const int CheckmateFrames = 150;
    private Point checkmateKing = new(-1, -1);
    private Side? checkmateWinner;
    private Move? checkmateFinalMove;
    private readonly List<MateParticle> checkmateParticles = new();

    private bool animating;
    private Piece? animatedPiece;
    private Move animatedMove;
    private int animationFrame;
    private int animationFrames = 14;
    private Piece? pendingPromotionPiece;
    private Side turnAfterAnimation;
    private HistoryEntry? pendingHistory;

    private int selectedTimeSeconds = 5 * 60;
    private int whiteSeconds = 5 * 60;
    private int blackSeconds = 5 * 60;
    private string statusText = "白方回合：請選擇棋子";

    private readonly Button newGameButton = new();
    private readonly Button undoButton = new();
    private readonly Button pauseButton = new();
    private readonly Button hintButton = new();
    private readonly Label titleLabel = new();
    private readonly Label statusLabel = new();
    private readonly Label whiteClockLabel = new();
    private readonly Label blackClockLabel = new();
    private readonly Label capturedWhiteLabel = new();
    private readonly Label capturedBlackLabel = new();
    private readonly Label hintLabel = new();
    private readonly Label modeLabel = new();
    private readonly CheckBox botCheckBox = new();
    private readonly CheckBox voiceCheckBox = new();
    private readonly ComboBox timeComboBox = new();
    private readonly ListBox moveListBox = new();

    public MainForm()
    {
        InitializeComponent();
        DoubleBuffered = true;
        Text = "Animated Chess Game - Enhanced";
        ClientSize = new Size(980, 700);
        MinimumSize = new Size(860, 620);
        BackColor = Color.FromArgb(49, 46, 43);
        Font = new Font("Microsoft JhengHei UI", 10F, FontStyle.Regular);
        KeyPreview = true;

        SetupUi();
        LoadPieceImages();
        LayoutUi();
        ResetGame();

        animationTimer.Interval = 16;
        animationTimer.Tick += AnimationTimer_Tick;

        clockTimer.Interval = 1000;
        clockTimer.Tick += ClockTimer_Tick;
        clockTimer.Start();

        botTimer.Interval = 650;
        botTimer.Tick += BotTimer_Tick;

        checkmateTimer.Interval = 20;
        checkmateTimer.Tick += CheckmateTimer_Tick;

        MouseDown += MainForm_MouseDown;
        Paint += MainForm_Paint;
        Resize += (_, _) => { LayoutUi(); Invalidate(); };
        KeyDown += MainForm_KeyDown;
    }

    private void SetupUi()
    {
        titleLabel.Text = "西洋棋 Chess";
        titleLabel.ForeColor = Color.White;
        titleLabel.Font = new Font("Microsoft JhengHei UI", 20F, FontStyle.Bold);
        titleLabel.AutoSize = false;
        Controls.Add(titleLabel);

        statusLabel.Text = statusText;
        statusLabel.ForeColor = Color.FromArgb(234, 234, 234);
        statusLabel.Font = new Font("Microsoft JhengHei UI", 12F, FontStyle.Bold);
        statusLabel.AutoSize = false;
        Controls.Add(statusLabel);

        whiteClockLabel.ForeColor = Color.White;
        whiteClockLabel.BackColor = Color.FromArgb(65, 65, 65);
        whiteClockLabel.Font = new Font("Consolas", 16F, FontStyle.Bold);
        whiteClockLabel.TextAlign = ContentAlignment.MiddleCenter;
        Controls.Add(whiteClockLabel);

        blackClockLabel.ForeColor = Color.White;
        blackClockLabel.BackColor = Color.FromArgb(65, 65, 65);
        blackClockLabel.Font = new Font("Consolas", 16F, FontStyle.Bold);
        blackClockLabel.TextAlign = ContentAlignment.MiddleCenter;
        Controls.Add(blackClockLabel);

        ConfigureButton(newGameButton, "新遊戲", Color.FromArgb(129, 182, 76));
        newGameButton.Click += (_, _) => ResetGame();
        Controls.Add(newGameButton);

        ConfigureButton(undoButton, "悔棋", Color.FromArgb(103, 151, 190));
        undoButton.Click += (_, _) => UndoMove();
        Controls.Add(undoButton);

        ConfigureButton(pauseButton, "暫停", Color.FromArgb(110, 110, 110));
        pauseButton.Click += (_, _) => TogglePause();
        Controls.Add(pauseButton);

        ConfigureButton(hintButton, "提示", Color.FromArgb(180, 128, 64));
        hintButton.Click += (_, _) => ShowHint();
        Controls.Add(hintButton);

        modeLabel.Text = "設定";
        modeLabel.ForeColor = Color.White;
        modeLabel.Font = new Font("Microsoft JhengHei UI", 11F, FontStyle.Bold);
        Controls.Add(modeLabel);

        botCheckBox.Text = "人機對戰：我方白棋，電腦黑棋";
        botCheckBox.ForeColor = Color.FromArgb(230, 230, 230);
        botCheckBox.AutoSize = true;
        botCheckBox.CheckedChanged += (_, _) =>
        {
            if (botCheckBox.Checked)
            {
                SpeakText("電腦黑棋已加入戰局。準備好被將軍了嗎？");
                ScheduleBotIfNeeded();
            }
        };
        Controls.Add(botCheckBox);

        voiceCheckBox.Text = "開啟嘲諷語音";
        voiceCheckBox.ForeColor = Color.FromArgb(230, 230, 230);
        voiceCheckBox.AutoSize = true;
        voiceCheckBox.Checked = true;
        Controls.Add(voiceCheckBox);

        timeComboBox.DropDownStyle = ComboBoxStyle.DropDownList;
        timeComboBox.Items.AddRange(new object[] { "3 分鐘", "5 分鐘", "10 分鐘", "15 分鐘" });
        timeComboBox.SelectedIndex = 1;
        timeComboBox.SelectedIndexChanged += (_, _) =>
        {
            selectedTimeSeconds = timeComboBox.SelectedIndex switch
            {
                0 => 3 * 60,
                1 => 5 * 60,
                2 => 10 * 60,
                _ => 15 * 60
            };
            if (history.Count == 0) ResetClocks();
        };
        Controls.Add(timeComboBox);

        capturedWhiteLabel.ForeColor = Color.FromArgb(235, 235, 235);
        capturedWhiteLabel.AutoSize = false;
        Controls.Add(capturedWhiteLabel);

        capturedBlackLabel.ForeColor = Color.FromArgb(235, 235, 235);
        capturedBlackLabel.AutoSize = false;
        Controls.Add(capturedBlackLabel);

        moveListBox.BackColor = Color.FromArgb(61, 58, 55);
        moveListBox.ForeColor = Color.FromArgb(238, 238, 238);
        moveListBox.BorderStyle = BorderStyle.FixedSingle;
        moveListBox.Font = new Font("Consolas", 10F, FontStyle.Regular);
        Controls.Add(moveListBox);

        hintLabel.Text = "操作：點棋子 → 點綠點移動。\r\n快捷鍵：Ctrl+Z 悔棋、N 新遊戲、Space 暫停、H 提示。\r\n\r\n新增功能：自適應棋盤、計時、悔棋、人機對戰、走棋紀錄、吃子統計、嘲諷語音。";
        hintLabel.ForeColor = Color.FromArgb(210, 210, 210);
        hintLabel.AutoSize = false;
        Controls.Add(hintLabel);
    }

    private static void ConfigureButton(Button button, string text, Color color)
    {
        button.Text = text;
        button.Font = new Font("Microsoft JhengHei UI", 10.5F, FontStyle.Bold);
        button.BackColor = color;
        button.ForeColor = Color.White;
        button.FlatStyle = FlatStyle.Flat;
        button.FlatAppearance.BorderSize = 0;
    }

    private void LayoutUi()
    {
        int availablePanelWidth = Math.Max(SidePanelMinWidth, Math.Min(360, ClientSize.Width / 3));
        int availableBoardWidth = Math.Max(360, ClientSize.Width - availablePanelWidth - Margin * 3);
        int availableBoardHeight = Math.Max(360, ClientSize.Height - Margin * 2);
        tile = Math.Max(45, Math.Min(availableBoardWidth, availableBoardHeight) / 8);
        boardPixels = tile * 8;
        boardLeft = Margin;
        boardTop = Margin + Math.Max(0, (availableBoardHeight - boardPixels) / 2);

        int panelX = boardLeft + boardPixels + Margin;
        int panelW = Math.Max(SidePanelMinWidth, ClientSize.Width - panelX - Margin);
        int y = Margin;

        titleLabel.SetBounds(panelX, y, panelW, 40); y += 50;
        statusLabel.SetBounds(panelX, y, panelW, 58); y += 68;

        int half = (panelW - 10) / 2;
        blackClockLabel.SetBounds(panelX, y, half, 45);
        whiteClockLabel.SetBounds(panelX + half + 10, y, half, 45);
        y += 56;

        newGameButton.SetBounds(panelX, y, half, 40);
        undoButton.SetBounds(panelX + half + 10, y, half, 40);
        y += 48;
        pauseButton.SetBounds(panelX, y, half, 40);
        hintButton.SetBounds(panelX + half + 10, y, half, 40);
        y += 54;

        modeLabel.SetBounds(panelX, y, panelW, 24); y += 28;
        timeComboBox.SetBounds(panelX, y, panelW, 30); y += 38;
        botCheckBox.SetBounds(panelX, y, panelW, 24); y += 28;
        voiceCheckBox.SetBounds(panelX, y, panelW, 24); y += 36;

        capturedWhiteLabel.SetBounds(panelX, y, panelW, 28); y += 28;
        capturedBlackLabel.SetBounds(panelX, y, panelW, 28); y += 38;

        int remaining = ClientSize.Height - y - 150;
        moveListBox.SetBounds(panelX, y, panelW, Math.Max(100, remaining));
        y += moveListBox.Height + 12;
        hintLabel.SetBounds(panelX, y, panelW, Math.Max(110, ClientSize.Height - y - Margin));

        UpdateClockLabels();
    }

    private void LoadPieceImages()
    {
        string baseDir = Path.Combine(AppContext.BaseDirectory, "Resources", "Pieces");
        string[,] names =
        {
            { "WhiteKing", "Chess_klt45.svg.png" }, { "WhiteQueen", "Chess_qlt45.svg.png" },
            { "WhiteRook", "Chess_rlt45.svg.png" }, { "WhiteBishop", "Chess_blt45.svg.png" },
            { "WhiteKnight", "Chess_nlt45.svg.png" }, { "WhitePawn", "Chess_plt45.svg.png" },
            { "BlackKing", "Chess_kdt45.svg.png" }, { "BlackQueen", "Chess_qdt45.svg.png" },
            { "BlackRook", "Chess_rdt45.svg.png" }, { "BlackBishop", "Chess_bdt45.svg.png" },
            { "BlackKnight", "Chess_ndt45.svg.png" }, { "BlackPawn", "Chess_pdt45.svg.png" }
        };

        foreach (var row in Enumerable.Range(0, names.GetLength(0)))
        {
            string key = names[row, 0];
            string file = Path.Combine(baseDir, names[row, 1]);
            if (File.Exists(file)) pieceImages[key] = Image.FromFile(file);
        }
    }

    private void ResetGame()
    {
        Array.Clear(board);
        currentTurn = Side.White;
        selected = null;
        legalMoves.Clear();
        lastMove = null;
        hintMove = null;
        history.Clear();
        capturedByWhite.Clear();
        capturedByBlack.Clear();
        moveListBox.Items.Clear();
        gameOver = false;
        paused = false;
        botThinking = false;
        animating = false;
        animationTimer.Stop();
        botTimer.Stop();
        checkmateTimer.Stop();
        checkmateAnimating = false;
        checkmateFrame = 0;
        checkmateWinner = null;
        checkmateFinalMove = null;
        checkmateKing = new Point(-1, -1);
        checkmateParticles.Clear();
        pauseButton.Text = "暫停";
        ResetClocks();

        board[0, 0] = new Piece(Side.Black, Kind.Rook);
        board[0, 1] = new Piece(Side.Black, Kind.Knight);
        board[0, 2] = new Piece(Side.Black, Kind.Bishop);
        board[0, 3] = new Piece(Side.Black, Kind.Queen);
        board[0, 4] = new Piece(Side.Black, Kind.King);
        board[0, 5] = new Piece(Side.Black, Kind.Bishop);
        board[0, 6] = new Piece(Side.Black, Kind.Knight);
        board[0, 7] = new Piece(Side.Black, Kind.Rook);
        for (int c = 0; c < 8; c++) board[1, c] = new Piece(Side.Black, Kind.Pawn);

        board[7, 0] = new Piece(Side.White, Kind.Rook);
        board[7, 1] = new Piece(Side.White, Kind.Knight);
        board[7, 2] = new Piece(Side.White, Kind.Bishop);
        board[7, 3] = new Piece(Side.White, Kind.Queen);
        board[7, 4] = new Piece(Side.White, Kind.King);
        board[7, 5] = new Piece(Side.White, Kind.Bishop);
        board[7, 6] = new Piece(Side.White, Kind.Knight);
        board[7, 7] = new Piece(Side.White, Kind.Rook);
        for (int c = 0; c < 8; c++) board[6, c] = new Piece(Side.White, Kind.Pawn);

        SetStatus("白方回合：請選擇棋子");
        UpdateCapturedLabels();
        Invalidate();
    }

    private void ResetClocks()
    {
        whiteSeconds = selectedTimeSeconds;
        blackSeconds = selectedTimeSeconds;
        UpdateClockLabels();
    }

    private void MainForm_MouseDown(object? sender, MouseEventArgs e)
    {
        if (gameOver || paused || animating || botThinking || IsBotTurn()) return;
        if (!PointToCell(e.Location, out int row, out int col)) return;

        if (TryFindMoveTo(row, col, out Move chosen))
        {
            StartMoveAnimation(chosen);
            return;
        }

        Piece? p = board[row, col];
        if (p is not null && p.Value.Side == currentTurn)
        {
            selected = new Point(col, row);
            legalMoves = GetLegalMovesForPiece(row, col);
            hintMove = null;
            PlaySound("select");
            Invalidate();
        }
        else
        {
            selected = null;
            legalMoves.Clear();
            hintMove = null;
            Invalidate();
        }
    }

    private bool TryFindMoveTo(int row, int col, out Move chosen)
    {
        foreach (Move move in legalMoves)
        {
            if (move.ToRow == row && move.ToCol == col)
            {
                chosen = move;
                return true;
            }
        }
        chosen = default;
        return false;
    }

    private void StartMoveAnimation(Move move)
    {
        Piece moving = board[move.FromRow, move.FromCol]!.Value;
        Piece? captured = board[move.ToRow, move.ToCol];

        pendingHistory = new HistoryEntry
        {
            Move = move,
            MovingPiece = moving,
            CapturedPiece = captured,
            PreviousTurn = currentTurn,
            PreviousLastMove = lastMove,
            PreviousWhiteSeconds = whiteSeconds,
            PreviousBlackSeconds = blackSeconds,
            PreviousGameOver = gameOver
        };

        animatedPiece = moving;
        animatedMove = move;
        animationFrame = 0;
        animationFrames = Math.Max(10, Math.Min(18, tile / 4));
        animating = true;
        turnAfterAnimation = Opposite(currentTurn);
        pendingPromotionPiece = null;
        hintMove = null;

        board[move.FromRow, move.FromCol] = null;
        board[move.ToRow, move.ToCol] = null;
        selected = null;
        legalMoves.Clear();

        if (moving.Kind == Kind.Pawn && (move.ToRow == 0 || move.ToRow == 7))
            pendingPromotionPiece = new Piece(moving.Side, Kind.Queen);

        PlaySound(captured is null ? "move" : "capture");
        animationTimer.Start();
        Invalidate();
    }

    private void AnimationTimer_Tick(object? sender, EventArgs e)
    {
        animationFrame++;
        if (animationFrame >= animationFrames)
        {
            animationTimer.Stop();
            animating = false;
            board[animatedMove.ToRow, animatedMove.ToCol] = pendingPromotionPiece ?? animatedPiece;
            lastMove = animatedMove;
            animatedPiece = null;
            currentTurn = turnAfterAnimation;

            GameStateInfo state = UpdateGameState();
            FinalizeHistoryEntry(state);
            UpdateCapturedLabels();
            ScheduleBotIfNeeded();
        }
        Invalidate();
    }

    private void FinalizeHistoryEntry(GameStateInfo state)
    {
        if (pendingHistory is null) return;

        if (pendingHistory.CapturedPiece is Piece captured)
        {
            if (pendingHistory.MovingPiece.Side == Side.White) capturedByWhite.Add(captured);
            else capturedByBlack.Add(captured);
        }

        pendingHistory.Notation = BuildNotation(pendingHistory, state);
        history.Add(pendingHistory);
        moveListBox.Items.Add(pendingHistory.Notation);
        moveListBox.TopIndex = Math.Max(0, moveListBox.Items.Count - 1);

        if (state.EndReason == GameEndReason.Checkmate)
            SpeakTaunt("checkmate", pendingHistory.MovingPiece.Side);
        else if (state.InCheck)
            SpeakTaunt("check", pendingHistory.MovingPiece.Side);
        else if (pendingHistory.CapturedPiece is not null)
            SpeakTaunt(pendingHistory.MovingPiece.Side == Side.Black && botCheckBox.Checked ? "botCapture" : "capture", pendingHistory.MovingPiece.Side);
        else if (pendingHistory.MovingPiece.Side == Side.Black && botCheckBox.Checked && rng.NextDouble() < 0.25)
            SpeakTaunt("botMove", pendingHistory.MovingPiece.Side);

        pendingHistory = null;
    }

    private string BuildNotation(HistoryEntry entry, GameStateInfo state)
    {
        int fullMove = history.Count / 2 + 1;
        string side = entry.MovingPiece.Side == Side.White ? "白" : "黑";
        string piece = entry.MovingPiece.Kind == Kind.Pawn ? "" : KindLetter(entry.MovingPiece.Kind);
        string sep = entry.CapturedPiece is null ? "-" : "x";
        string promotion = entry.MovingPiece.Kind == Kind.Pawn && (entry.Move.ToRow == 0 || entry.Move.ToRow == 7) ? "=Q" : "";
        string suffix = state.EndReason == GameEndReason.Checkmate ? "#" : state.InCheck ? "+" : "";
        return $"{fullMove,2}. {side} {piece}{CellName(entry.Move.FromRow, entry.Move.FromCol)}{sep}{CellName(entry.Move.ToRow, entry.Move.ToCol)}{promotion}{suffix}";
    }

    private GameStateInfo UpdateGameState()
    {
        bool inCheck = IsKingInCheck(currentTurn);
        bool hasMove = HasAnyLegalMove(currentTurn);
        GameEndReason reason = GameEndReason.None;

        if (inCheck && !hasMove)
        {
            gameOver = true;
            reason = GameEndReason.Checkmate;
            PlaySound("gameover");
            SetStatus($"將死！{SideName(Opposite(currentTurn))}獲勝");
            StartCheckmateAnimation(Opposite(currentTurn), currentTurn);
        }
        else if (!inCheck && !hasMove)
        {
            gameOver = true;
            reason = GameEndReason.Stalemate;
            PlaySound("gameover");
            SetStatus("和棋：無合法步可走");
        }
        else if (inCheck)
        {
            PlaySound("check");
            SetStatus($"{SideName(currentTurn)}回合：被將軍！");
        }
        else
        {
            SetStatus($"{SideName(currentTurn)}回合：請選擇棋子");
        }

        return new GameStateInfo { InCheck = inCheck, HasMove = hasMove, EndReason = reason };
    }

    private void UndoMove()
    {
        if (animating || history.Count == 0) return;

        int undoCount = botCheckBox.Checked && currentTurn == Side.White && history.Count >= 2 ? 2 : 1;
        for (int i = 0; i < undoCount && history.Count > 0; i++)
            UndoOneMove();

        botTimer.Stop();
        botThinking = false;
        checkmateTimer.Stop();
        checkmateAnimating = false;
        checkmateFrame = 0;
        checkmateWinner = null;
        checkmateFinalMove = null;
        checkmateKing = new Point(-1, -1);
        checkmateParticles.Clear();
        selected = null;
        legalMoves.Clear();
        hintMove = null;
        paused = false;
        pauseButton.Text = "暫停";
        SetStatus($"已悔棋，{SideName(currentTurn)}回合");
        UpdateCapturedLabels();
        UpdateClockLabels();
        PlaySound("select");
        SpeakTaunt("undo", currentTurn);
        Invalidate();
    }

    private void UndoOneMove()
    {
        HistoryEntry entry = history[^1];
        history.RemoveAt(history.Count - 1);
        if (moveListBox.Items.Count > 0) moveListBox.Items.RemoveAt(moveListBox.Items.Count - 1);

        board[entry.Move.FromRow, entry.Move.FromCol] = entry.MovingPiece;
        board[entry.Move.ToRow, entry.Move.ToCol] = entry.CapturedPiece;
        currentTurn = entry.PreviousTurn;
        lastMove = entry.PreviousLastMove;
        whiteSeconds = entry.PreviousWhiteSeconds;
        blackSeconds = entry.PreviousBlackSeconds;
        gameOver = entry.PreviousGameOver;

        if (entry.CapturedPiece is not null)
        {
            List<Piece> list = entry.MovingPiece.Side == Side.White ? capturedByWhite : capturedByBlack;
            if (list.Count > 0) list.RemoveAt(list.Count - 1);
        }
    }

    private void ClockTimer_Tick(object? sender, EventArgs e)
    {
        if (gameOver || paused || animating) return;

        if (currentTurn == Side.White) whiteSeconds--;
        else blackSeconds--;
        UpdateClockLabels();

        if (whiteSeconds <= 0 || blackSeconds <= 0)
        {
            gameOver = true;
            botTimer.Stop();
            Side winner = whiteSeconds <= 0 ? Side.Black : Side.White;
            PlaySound("gameover");
            SetStatus($"時間到！{SideName(winner)}獲勝");
            SpeakTaunt("timeout", winner);
            Invalidate();
        }
    }

    private void UpdateClockLabels()
    {
        whiteClockLabel.Text = $"白 {FormatTime(whiteSeconds)}";
        blackClockLabel.Text = $"黑 {FormatTime(blackSeconds)}";
        whiteClockLabel.BackColor = currentTurn == Side.White && !gameOver ? Color.FromArgb(96, 130, 76) : Color.FromArgb(65, 65, 65);
        blackClockLabel.BackColor = currentTurn == Side.Black && !gameOver ? Color.FromArgb(96, 130, 76) : Color.FromArgb(65, 65, 65);
    }

    private static string FormatTime(int seconds)
    {
        seconds = Math.Max(0, seconds);
        return $"{seconds / 60:00}:{seconds % 60:00}";
    }

    private void TogglePause()
    {
        if (gameOver) return;
        paused = !paused;
        pauseButton.Text = paused ? "繼續" : "暫停";
        if (paused)
        {
            botTimer.Stop();
            SetStatus("遊戲已暫停");
        }
        else
        {
            SetStatus($"{SideName(currentTurn)}回合：請選擇棋子");
            ScheduleBotIfNeeded();
        }
        Invalidate();
    }

    private bool IsBotTurn() => botCheckBox.Checked && currentTurn == Side.Black && !gameOver && !paused;

    private void ScheduleBotIfNeeded()
    {
        UpdateClockLabels();
        if (!IsBotTurn() || animating) return;
        botThinking = true;
        SetStatus("黑方電腦思考中...");
        botTimer.Stop();
        botTimer.Start();
    }

    private void BotTimer_Tick(object? sender, EventArgs e)
    {
        botTimer.Stop();
        if (!IsBotTurn() || animating)
        {
            botThinking = false;
            return;
        }

        List<Move> moves = GetAllLegalMoves(Side.Black).ToList();
        if (moves.Count == 0)
        {
            botThinking = false;
            UpdateGameState();
            return;
        }

        Move bestMove = ChooseSmartMove(Side.Black);
        botThinking = false;
        StartMoveAnimation(bestMove);
    }

    private void ShowHint()
    {
        if (gameOver || paused || animating || botThinking || IsBotTurn()) return;
        List<Move> moves = GetAllLegalMoves(currentTurn).ToList();
        if (moves.Count == 0) return;

        hintMove = ChooseSmartMove(currentTurn);
        SetStatus($"提示：建議 {CellName(hintMove.Value.FromRow, hintMove.Value.FromCol)} → {CellName(hintMove.Value.ToRow, hintMove.Value.ToCol)}");
        Invalidate();
    }

    private Move ChooseSmartMove(Side side)
    {
        List<Move> moves = GetAllLegalMoves(side).ToList();
        if (moves.Count == 0) return default;

        Move best = moves[0];
        double bestScore = double.NegativeInfinity;

        foreach (Move move in moves)
        {
            double score = ScoreMoveShape(move, side);
            SearchUndo undo = ApplySearchMove(move);

            if (IsKingInCheck(Opposite(side))) score += 35;
            if (IsKingInCheck(Opposite(side)) && !HasAnyLegalMove(Opposite(side))) score += 100000;
            score += EvaluateBoard(side);

            List<Move> replies = GetAllLegalMoves(Opposite(side)).ToList();
            if (replies.Count > 0)
            {
                double bestReply = double.NegativeInfinity;
                foreach (Move reply in replies)
                {
                    double replyScore = ScoreMoveShape(reply, Opposite(side));
                    SearchUndo replyUndo = ApplySearchMove(reply);
                    replyScore += EvaluateBoard(Opposite(side));
                    if (IsKingInCheck(side)) replyScore += 35;
                    UndoSearchMove(replyUndo);
                    bestReply = Math.Max(bestReply, replyScore);
                }
                score -= bestReply * 0.62;
            }

            score += rng.NextDouble() * 0.2;
            UndoSearchMove(undo);

            if (score > bestScore)
            {
                bestScore = score;
                best = move;
            }
        }

        return best;
    }

    private double ScoreMoveShape(Move move, Side side)
    {
        Piece moving = board[move.FromRow, move.FromCol]!.Value;
        Piece? captured = board[move.ToRow, move.ToCol];
        double score = 0;

        if (captured is not null)
            score += PieceValue(captured.Value.Kind) * 1.25 - PieceValue(moving.Kind) * 0.08;

        if (moving.Kind == Kind.Pawn && (move.ToRow == 0 || move.ToRow == 7)) score += PieceValue(Kind.Queen) - PieceValue(Kind.Pawn);

        double centerDistance = Math.Abs(3.5 - move.ToRow) + Math.Abs(3.5 - move.ToCol);
        score += (7 - centerDistance) * 0.35;

        if (moving.Kind == Kind.Knight || moving.Kind == Kind.Bishop)
        {
            int homeRow = moving.Side == Side.White ? 7 : 0;
            if (move.FromRow == homeRow) score += 1.2;
        }

        if (moving.Kind == Kind.Queen && history.Count < 8) score -= 0.8;
        if (moving.Kind == Kind.King && history.Count < 12) score -= 0.6;

        return score;
    }

    private double EvaluateBoard(Side perspective)
    {
        double score = 0;
        for (int r = 0; r < 8; r++)
        for (int c = 0; c < 8; c++)
        {
            Piece? p = board[r, c];
            if (p is null) continue;
            double value = PieceValue(p.Value.Kind);
            double center = 0.05 * (7 - (Math.Abs(3.5 - r) + Math.Abs(3.5 - c)));
            double signed = value + center;
            score += p.Value.Side == perspective ? signed : -signed;
        }
        return score;
    }

    private static int PieceValue(Kind kind) => kind switch
    {
        Kind.Pawn => 100,
        Kind.Knight => 320,
        Kind.Bishop => 330,
        Kind.Rook => 500,
        Kind.Queen => 900,
        Kind.King => 20000,
        _ => 0
    };

    private SearchUndo ApplySearchMove(Move move)
    {
        Piece moving = board[move.FromRow, move.FromCol]!.Value;
        Piece? captured = board[move.ToRow, move.ToCol];
        Piece placed = moving.Kind == Kind.Pawn && (move.ToRow == 0 || move.ToRow == 7)
            ? new Piece(moving.Side, Kind.Queen)
            : moving;
        board[move.FromRow, move.FromCol] = null;
        board[move.ToRow, move.ToCol] = placed;
        return new SearchUndo(move, moving, captured);
    }

    private void UndoSearchMove(SearchUndo undo)
    {
        board[undo.Move.FromRow, undo.Move.FromCol] = undo.MovingPiece;
        board[undo.Move.ToRow, undo.Move.ToCol] = undo.CapturedPiece;
    }

    private void MainForm_Paint(object? sender, PaintEventArgs e)
    {
        Graphics g = e.Graphics;
        g.SmoothingMode = SmoothingMode.AntiAlias;
        DrawBoard(g);
        DrawHighlights(g);
        DrawPieces(g);
        DrawAnimation(g);
        DrawCoordinates(g);
        if (checkmateWinner is not null) DrawCheckmateAnimation(g);
        if (paused) DrawPausedOverlay(g);
    }

    private void DrawBoard(Graphics g)
    {
        Color light = Color.FromArgb(238, 238, 210);
        Color dark = Color.FromArgb(118, 150, 86);
        using Pen border = new(Color.FromArgb(30, 30, 30), Math.Max(2, tile / 32));

        for (int r = 0; r < 8; r++)
        for (int c = 0; c < 8; c++)
        {
            using SolidBrush brush = new((r + c) % 2 == 0 ? light : dark);
            g.FillRectangle(brush, CellRect(r, c));
        }
        g.DrawRectangle(border, boardLeft, boardTop, boardPixels, boardPixels);
    }

    private void DrawHighlights(Graphics g)
    {
        if (lastMove is not null)
        {
            using SolidBrush b = new(Color.FromArgb(115, 255, 235, 59));
            g.FillRectangle(b, CellRect(lastMove.Value.FromRow, lastMove.Value.FromCol));
            g.FillRectangle(b, CellRect(lastMove.Value.ToRow, lastMove.Value.ToCol));
        }

        if (hintMove is not null)
        {
            using Pen hintPen = new(Color.FromArgb(230, 60, 190, 255), Math.Max(3, tile / 14));
            g.DrawRectangle(hintPen, Inflate(CellRect(hintMove.Value.FromRow, hintMove.Value.FromCol), -4));
            g.DrawRectangle(hintPen, Inflate(CellRect(hintMove.Value.ToRow, hintMove.Value.ToCol), -4));
        }

        if (selected is not null)
        {
            using SolidBrush sel = new(Color.FromArgb(150, 255, 235, 59));
            g.FillRectangle(sel, CellRect(selected.Value.Y, selected.Value.X));
        }

        foreach (Move m in legalMoves)
        {
            Rectangle rc = CellRect(m.ToRow, m.ToCol);
            if (board[m.ToRow, m.ToCol] is null)
            {
                using SolidBrush dot = new(Color.FromArgb(130, 40, 40, 40));
                int size = Math.Max(14, tile / 4);
                g.FillEllipse(dot, rc.Left + tile / 2 - size / 2, rc.Top + tile / 2 - size / 2, size, size);
            }
            else
            {
                using Pen ring = new(Color.FromArgb(180, 180, 30, 30), Math.Max(3, tile / 14));
                int inset = Math.Max(7, tile / 9);
                g.DrawEllipse(ring, rc.Left + inset, rc.Top + inset, tile - inset * 2, tile - inset * 2);
            }
        }

        Point whiteKing = FindKing(Side.White);
        Point blackKing = FindKing(Side.Black);
        if (whiteKing.X >= 0 && IsKingInCheck(Side.White)) DrawKingWarning(g, whiteKing.Y, whiteKing.X);
        if (blackKing.X >= 0 && IsKingInCheck(Side.Black)) DrawKingWarning(g, blackKing.Y, blackKing.X);
    }

    private static Rectangle Inflate(Rectangle rect, int value)
    {
        rect.Inflate(value, value);
        return rect;
    }

    private void DrawKingWarning(Graphics g, int row, int col)
    {
        Rectangle rc = CellRect(row, col);
        using SolidBrush brush = new(Color.FromArgb(100, 210, 40, 40));
        g.FillRectangle(brush, rc);
    }

    private void DrawPieces(Graphics g)
    {
        for (int r = 0; r < 8; r++)
        for (int c = 0; c < 8; c++)
        {
            Piece? p = board[r, c];
            if (p is not null) DrawPiece(g, p.Value, CellRect(r, c));
        }
    }

    private void DrawAnimation(Graphics g)
    {
        if (!animating || animatedPiece is null) return;
        float t = Math.Min(1f, animationFrame / (float)animationFrames);
        t = 1f - (1f - t) * (1f - t); // ease out

        Rectangle from = CellRect(animatedMove.FromRow, animatedMove.FromCol);
        Rectangle to = CellRect(animatedMove.ToRow, animatedMove.ToCol);
        int x = (int)(from.Left + (to.Left - from.Left) * t);
        int y = (int)(from.Top + (to.Top - from.Top) * t);
        DrawPiece(g, animatedPiece.Value, new Rectangle(x, y, tile, tile));
    }

    private void DrawPiece(Graphics g, Piece piece, Rectangle cell)
    {
        string key = piece.Side + piece.Kind.ToString();
        int mx = Math.Max(5, tile / 9);
        int my = Math.Max(4, tile / 11);
        Rectangle imgRect = new(cell.Left + mx, cell.Top + my, tile - mx * 2, tile - my * 2);
        if (pieceImages.TryGetValue(key, out Image? img))
        {
            g.DrawImage(img, imgRect);
        }
        else
        {
            using SolidBrush b = new(piece.Side == Side.White ? Color.White : Color.Black);
            using StringFormat sf = new() { Alignment = StringAlignment.Center, LineAlignment = StringAlignment.Center };
            using Font f = new(Font.FontFamily, Math.Max(20, tile * 0.45f), FontStyle.Bold);
            g.DrawString(piece.Kind.ToString()[0].ToString(), f, b, cell, sf);
        }
    }

    private void DrawCoordinates(Graphics g)
    {
        using Font f = new("Segoe UI", Math.Max(8F, tile * 0.13F), FontStyle.Bold);
        for (int i = 0; i < 8; i++)
        {
            string file = ((char)('a' + i)).ToString();
            string rank = (8 - i).ToString();
            using SolidBrush b1 = new(i % 2 == 0 ? Color.FromArgb(118, 150, 86) : Color.FromArgb(238, 238, 210));
            g.DrawString(file, f, b1, boardLeft + i * tile + tile - Math.Max(18, tile / 4), boardTop + boardPixels - Math.Max(20, tile / 4));
            g.DrawString(rank, f, b1, boardLeft + Math.Max(4, tile / 15), boardTop + i * tile + Math.Max(2, tile / 18));
        }
    }

    private void StartCheckmateAnimation(Side winner, Side loser)
    {
        checkmateWinner = winner;
        checkmateKing = FindKing(loser);
        checkmateFinalMove = lastMove;
        checkmateFrame = 0;
        checkmateAnimating = true;
        checkmateParticles.Clear();

        Rectangle focus = checkmateKing.X >= 0
            ? CellRect(checkmateKing.Y, checkmateKing.X)
            : new Rectangle(boardLeft + boardPixels / 2 - tile / 2, boardTop + boardPixels / 2 - tile / 2, tile, tile);

        float cx = focus.Left + focus.Width / 2f;
        float cy = focus.Top + focus.Height / 2f;
        for (int i = 0; i < 90; i++)
        {
            double angle = rng.NextDouble() * Math.PI * 2;
            double speed = 1.2 + rng.NextDouble() * 4.2;
            float life = 45 + rng.Next(0, 55);
            checkmateParticles.Add(new MateParticle
            {
                X = cx,
                Y = cy,
                Vx = (float)(Math.Cos(angle) * speed),
                Vy = (float)(Math.Sin(angle) * speed - 1.2),
                Size = 3 + (float)rng.NextDouble() * 6,
                Life = life,
                MaxLife = life
            });
        }

        checkmateTimer.Stop();
        checkmateTimer.Start();
        Invalidate();
    }

    private void CheckmateTimer_Tick(object? sender, EventArgs e)
    {
        checkmateFrame++;

        foreach (MateParticle p in checkmateParticles)
        {
            p.X += p.Vx;
            p.Y += p.Vy;
            p.Vx *= 0.985f;
            p.Vy = p.Vy * 0.985f + 0.055f;
            p.Life--;
        }
        checkmateParticles.RemoveAll(p => p.Life <= 0);

        if (checkmateFrame >= CheckmateFrames)
        {
            checkmateAnimating = false;
            checkmateTimer.Stop();
        }
        Invalidate();
    }

    private void DrawCheckmateAnimation(Graphics g)
    {
        float progress = checkmateAnimating ? Math.Min(1f, checkmateFrame / (float)CheckmateFrames) : 1f;
        float intro = EaseOut(Math.Min(1f, progress * 2.6f));
        Rectangle boardRect = new(boardLeft, boardTop, boardPixels, boardPixels);

        using (SolidBrush dim = new(Color.FromArgb((int)(25 + 105 * intro), 0, 0, 0)))
            g.FillRectangle(dim, boardRect);

        DrawCheckmateFinalPath(g, intro);
        DrawCheckmateKingFocus(g, intro);
        DrawCheckmateParticles(g);
        DrawCheckmateBanner(g, intro);
    }

    private void DrawCheckmateFinalPath(Graphics g, float intro)
    {
        if (checkmateFinalMove is null) return;

        PointF from = CellCenter(checkmateFinalMove.Value.FromRow, checkmateFinalMove.Value.FromCol);
        PointF to = CellCenter(checkmateFinalMove.Value.ToRow, checkmateFinalMove.Value.ToCol);
        float pulse = 0.5f + 0.5f * (float)Math.Sin(checkmateFrame * 0.18f);
        int width = Math.Max(5, tile / 12) + (int)(pulse * Math.Max(4, tile / 18));
        int alpha = (int)(170 * intro);

        using Pen glow = new(Color.FromArgb(alpha, 255, 224, 80), width)
        {
            StartCap = LineCap.Round,
            EndCap = LineCap.ArrowAnchor
        };
        g.DrawLine(glow, from, to);

        using Pen core = new(Color.FromArgb((int)(230 * intro), 255, 248, 180), Math.Max(2, width / 3))
        {
            StartCap = LineCap.Round,
            EndCap = LineCap.Round
        };
        g.DrawLine(core, from, to);
    }

    private void DrawCheckmateKingFocus(Graphics g, float intro)
    {
        if (checkmateKing.X < 0) return;

        Rectangle rc = CellRect(checkmateKing.Y, checkmateKing.X);
        PointF center = CellCenter(checkmateKing.Y, checkmateKing.X);
        float pulse = 0.5f + 0.5f * (float)Math.Sin(checkmateFrame * 0.24f);
        int ringSize = (int)(tile * (1.15f + 0.35f * pulse));
        Rectangle ring = new((int)(center.X - ringSize / 2f), (int)(center.Y - ringSize / 2f), ringSize, ringSize);

        using SolidBrush danger = new(Color.FromArgb((int)(90 * intro), 210, 34, 34));
        g.FillRectangle(danger, rc);

        using Pen outer = new(Color.FromArgb((int)(235 * intro), 255, 70, 70), Math.Max(4, tile / 13));
        g.DrawEllipse(outer, ring);

        using Pen cross = new(Color.FromArgb((int)(230 * intro), 255, 230, 230), Math.Max(3, tile / 16));
        {
            int inset = Math.Max(12, tile / 5);
            g.DrawLine(cross, rc.Left + inset, rc.Top + inset, rc.Right - inset, rc.Bottom - inset);
            g.DrawLine(cross, rc.Right - inset, rc.Top + inset, rc.Left + inset, rc.Bottom - inset);
        }
    }

    private void DrawCheckmateParticles(Graphics g)
    {
        foreach (MateParticle p in checkmateParticles)
        {
            float ratio = Math.Max(0f, Math.Min(1f, p.Life / p.MaxLife));
            int alpha = (int)(220 * ratio);
            using SolidBrush spark = new(Color.FromArgb(alpha, 255, 220, 80));
            g.FillEllipse(spark, p.X - p.Size / 2f, p.Y - p.Size / 2f, p.Size, p.Size);
        }
    }

    private void DrawCheckmateBanner(Graphics g, float intro)
    {
        string winnerName = checkmateWinner is null ? "" : SideName(checkmateWinner.Value);
        string title = "將死 CHECKMATE";
        string subtitle = $"{winnerName}獲勝｜按 N 重新開始，或 Ctrl+Z 悔棋";

        int w = (int)(boardPixels * 0.82f);
        int h = Math.Max(112, tile + 42);
        int x = boardLeft + (boardPixels - w) / 2;
        int y = boardTop + (int)(boardPixels * 0.34f);
        y += (int)((1f - intro) * 42);
        Rectangle shadowRect = new(x + 8, y + 8, w, h);
        Rectangle bannerRect = new(x, y, w, h);

        using GraphicsPath shadowPath = RoundedRect(shadowRect, Math.Max(18, tile / 4));
        using SolidBrush shadow = new(Color.FromArgb((int)(135 * intro), 0, 0, 0));
        g.FillPath(shadow, shadowPath);

        using GraphicsPath bannerPath = RoundedRect(bannerRect, Math.Max(18, tile / 4));
        using LinearGradientBrush bg = new(bannerRect, Color.FromArgb((int)(235 * intro), 70, 48, 42), Color.FromArgb((int)(235 * intro), 33, 33, 33), LinearGradientMode.Horizontal);
        using Pen border = new(Color.FromArgb((int)(230 * intro), 255, 220, 120), Math.Max(3, tile / 18));
        g.FillPath(bg, bannerPath);
        g.DrawPath(border, bannerPath);

        using StringFormat sf = new() { Alignment = StringAlignment.Center, LineAlignment = StringAlignment.Center };
        using Font titleFont = new("Microsoft JhengHei UI", Math.Max(22f, tile * 0.42f), FontStyle.Bold);
        using Font subFont = new("Microsoft JhengHei UI", Math.Max(10f, tile * 0.17f), FontStyle.Bold);

        Rectangle titleRect = new(bannerRect.Left, bannerRect.Top + 15, bannerRect.Width, bannerRect.Height / 2);
        Rectangle subRect = new(bannerRect.Left + 10, bannerRect.Top + bannerRect.Height / 2 + 20, bannerRect.Width - 20, bannerRect.Height / 3);
        using SolidBrush titleBrush = new(Color.FromArgb((int)(255 * intro), 255, 246, 210));
        using SolidBrush subBrush = new(Color.FromArgb((int)(230 * intro), 245, 245, 245));
        g.DrawString(title, titleFont, titleBrush, titleRect, sf);
        g.DrawString(subtitle, subFont, subBrush, subRect, sf);
    }

    private PointF CellCenter(int row, int col)
    {
        Rectangle rc = CellRect(row, col);
        return new PointF(rc.Left + rc.Width / 2f, rc.Top + rc.Height / 2f);
    }

    private static float EaseOut(float t)
    {
        t = Math.Max(0f, Math.Min(1f, t));
        return 1f - (1f - t) * (1f - t) * (1f - t);
    }

    private static GraphicsPath RoundedRect(Rectangle bounds, int radius)
    {
        int d = radius * 2;
        GraphicsPath path = new();
        path.AddArc(bounds.Left, bounds.Top, d, d, 180, 90);
        path.AddArc(bounds.Right - d, bounds.Top, d, d, 270, 90);
        path.AddArc(bounds.Right - d, bounds.Bottom - d, d, d, 0, 90);
        path.AddArc(bounds.Left, bounds.Bottom - d, d, d, 90, 90);
        path.CloseFigure();
        return path;
    }

    private void DrawPausedOverlay(Graphics g)
    {
        Rectangle rc = new(boardLeft, boardTop, boardPixels, boardPixels);
        using SolidBrush cover = new(Color.FromArgb(130, 0, 0, 0));
        using StringFormat sf = new() { Alignment = StringAlignment.Center, LineAlignment = StringAlignment.Center };
        using Font f = new("Microsoft JhengHei UI", Math.Max(28, tile / 2), FontStyle.Bold);
        g.FillRectangle(cover, rc);
        g.DrawString("暫停", f, Brushes.White, rc, sf);
    }

    private List<Move> GetLegalMovesForPiece(int row, int col)
    {
        Piece? p = board[row, col];
        if (p is null) return new List<Move>();

        List<Move> result = new();
        foreach (Move m in GetPseudoMoves(row, col, board))
        {
            if (board[m.ToRow, m.ToCol]?.Kind == Kind.King) continue;
            Piece? captured = board[m.ToRow, m.ToCol];
            Piece moving = board[m.FromRow, m.FromCol]!.Value;
            Piece placed = moving.Kind == Kind.Pawn && (m.ToRow == 0 || m.ToRow == 7)
                ? new Piece(moving.Side, Kind.Queen)
                : moving;
            board[m.ToRow, m.ToCol] = placed;
            board[m.FromRow, m.FromCol] = null;
            bool safe = !IsKingInCheck(p.Value.Side);
            board[m.FromRow, m.FromCol] = moving;
            board[m.ToRow, m.ToCol] = captured;
            if (safe) result.Add(m);
        }
        return result;
    }

    private IEnumerable<Move> GetAllLegalMoves(Side side)
    {
        for (int r = 0; r < 8; r++)
        for (int c = 0; c < 8; c++)
            if (board[r, c] is Piece p && p.Side == side)
                foreach (Move m in GetLegalMovesForPiece(r, c))
                    yield return m;
    }

    private IEnumerable<Move> GetPseudoMoves(int row, int col, Piece?[,] b)
    {
        Piece piece = b[row, col]!.Value;
        int dir = piece.Side == Side.White ? -1 : 1;

        bool AddIfValid(int r, int c, List<Move> list)
        {
            if (!Inside(r, c)) return false;
            if (b[r, c] is null)
            {
                list.Add(new Move(row, col, r, c));
                return true;
            }
            if (b[r, c]!.Value.Side != piece.Side)
                list.Add(new Move(row, col, r, c));
            return false;
        }

        List<Move> moves = new();
        switch (piece.Kind)
        {
            case Kind.Pawn:
                int one = row + dir;
                if (Inside(one, col) && b[one, col] is null)
                {
                    moves.Add(new Move(row, col, one, col));
                    int startRow = piece.Side == Side.White ? 6 : 1;
                    int two = row + dir * 2;
                    if (row == startRow && Inside(two, col) && b[two, col] is null)
                        moves.Add(new Move(row, col, two, col));
                }
                foreach (int dc in new[] { -1, 1 })
                {
                    int nr = row + dir, nc = col + dc;
                    if (Inside(nr, nc) && b[nr, nc] is not null && b[nr, nc]!.Value.Side != piece.Side)
                        moves.Add(new Move(row, col, nr, nc));
                }
                break;

            case Kind.Knight:
                int[,] k = { { -2, -1 }, { -2, 1 }, { -1, -2 }, { -1, 2 }, { 1, -2 }, { 1, 2 }, { 2, -1 }, { 2, 1 } };
                for (int i = 0; i < k.GetLength(0); i++) AddIfValid(row + k[i, 0], col + k[i, 1], moves);
                break;

            case Kind.King:
                for (int dr = -1; dr <= 1; dr++)
                for (int dc = -1; dc <= 1; dc++)
                    if (dr != 0 || dc != 0) AddIfValid(row + dr, col + dc, moves);
                break;

            case Kind.Rook:
                AddSliding(row, col, piece.Side, moves, b, new[] { (1, 0), (-1, 0), (0, 1), (0, -1) });
                break;
            case Kind.Bishop:
                AddSliding(row, col, piece.Side, moves, b, new[] { (1, 1), (1, -1), (-1, 1), (-1, -1) });
                break;
            case Kind.Queen:
                AddSliding(row, col, piece.Side, moves, b, new[] { (1, 0), (-1, 0), (0, 1), (0, -1), (1, 1), (1, -1), (-1, 1), (-1, -1) });
                break;
        }
        return moves;
    }

    private static void AddSliding(int row, int col, Side side, List<Move> moves, Piece?[,] b, IEnumerable<(int dr, int dc)> dirs)
    {
        foreach ((int dr, int dc) in dirs)
        {
            int r = row + dr, c = col + dc;
            while (Inside(r, c))
            {
                if (b[r, c] is null)
                    moves.Add(new Move(row, col, r, c));
                else
                {
                    if (b[r, c]!.Value.Side != side)
                        moves.Add(new Move(row, col, r, c));
                    break;
                }
                r += dr;
                c += dc;
            }
        }
    }

    private bool IsKingInCheck(Side side)
    {
        Point king = FindKing(side);
        if (king.X < 0) return true;
        return IsSquareAttacked(king.Y, king.X, Opposite(side));
    }

    private Point FindKing(Side side)
    {
        for (int r = 0; r < 8; r++)
        for (int c = 0; c < 8; c++)
            if (board[r, c] is Piece p && p.Side == side && p.Kind == Kind.King)
                return new Point(c, r);
        return new Point(-1, -1);
    }

    private bool IsSquareAttacked(int row, int col, Side bySide)
    {
        for (int r = 0; r < 8; r++)
        for (int c = 0; c < 8; c++)
        {
            Piece? p = board[r, c];
            if (p is null || p.Value.Side != bySide) continue;
            if (AttacksSquare(r, c, row, col, p.Value)) return true;
        }
        return false;
    }

    private bool AttacksSquare(int fromRow, int fromCol, int targetRow, int targetCol, Piece p)
    {
        int dr = targetRow - fromRow;
        int dc = targetCol - fromCol;
        int adr = Math.Abs(dr), adc = Math.Abs(dc);

        return p.Kind switch
        {
            Kind.Pawn => dr == (p.Side == Side.White ? -1 : 1) && adc == 1,
            Kind.Knight => (adr == 2 && adc == 1) || (adr == 1 && adc == 2),
            Kind.King => Math.Max(adr, adc) == 1,
            Kind.Rook => (dr == 0 || dc == 0) && PathClear(fromRow, fromCol, targetRow, targetCol),
            Kind.Bishop => adr == adc && PathClear(fromRow, fromCol, targetRow, targetCol),
            Kind.Queen => (dr == 0 || dc == 0 || adr == adc) && PathClear(fromRow, fromCol, targetRow, targetCol),
            _ => false
        };
    }

    private bool PathClear(int r1, int c1, int r2, int c2)
    {
        int dr = Math.Sign(r2 - r1);
        int dc = Math.Sign(c2 - c1);
        int r = r1 + dr, c = c1 + dc;
        while (r != r2 || c != c2)
        {
            if (board[r, c] is not null) return false;
            r += dr;
            c += dc;
        }
        return true;
    }

    private bool HasAnyLegalMove(Side side)
    {
        for (int r = 0; r < 8; r++)
        for (int c = 0; c < 8; c++)
            if (board[r, c] is Piece p && p.Side == side && GetLegalMovesForPiece(r, c).Count > 0)
                return true;
        return false;
    }

    private void UpdateCapturedLabels()
    {
        capturedWhiteLabel.Text = "白方吃子：" + CapturedText(capturedByWhite);
        capturedBlackLabel.Text = "黑方吃子：" + CapturedText(capturedByBlack);
    }

    private string CapturedText(List<Piece> pieces)
    {
        if (pieces.Count == 0) return "無";
        return string.Join(" ", pieces.Select(PieceSymbol));
    }

    private static string PieceSymbol(Piece p) => p.Side switch
    {
        Side.White => p.Kind switch
        {
            Kind.King => "♔",
            Kind.Queen => "♕",
            Kind.Rook => "♖",
            Kind.Bishop => "♗",
            Kind.Knight => "♘",
            Kind.Pawn => "♙",
            _ => "?"
        },
        _ => p.Kind switch
        {
            Kind.King => "♚",
            Kind.Queen => "♛",
            Kind.Rook => "♜",
            Kind.Bishop => "♝",
            Kind.Knight => "♞",
            Kind.Pawn => "♟",
            _ => "?"
        }
    };

    private void SpeakTaunt(string kind, Side side)
    {
        if (!voiceCheckBox.Checked) return;

        string[] lines = kind switch
        {
            "botCapture" => new[]
            {
                "謝謝你的棋子，我就收下了。",
                "這步你沒看到吧？",
                "你的防線有點鬆喔。"
            },
            "capture" => new[]
            {
                "有兩下子喔。",
                "不錯，這一步有水準。",
                "好吧，這顆算你拿得漂亮。"
            },
            "check" => new[]
            {
                "將軍！你的王很危險喔。",
                "小心，王快沒地方跑了。",
                "壓力來了，想清楚再走。"
            },
            "checkmate" => new[]
            {
                "將死！這局結束。",
                "漂亮收尾，國王無路可逃。",
                "勝負已定，下次再挑戰吧。"
            },
            "botMove" => new[]
            {
                "我已經算好了。",
                "輪到你了，別走錯喔。",
                "這一步只是熱身。"
            },
            "timeout" => new[]
            {
                "時間到，思考太久也是會輸的。",
                "鐘聲響起，勝負已定。"
            },
            "undo" => new[]
            {
                "悔棋成功，這次想清楚一點。",
                "重新來過也可以，但我會記得。"
            },
            _ => new[] { side == Side.White ? "白方行動。" : "黑方行動。" }
        };

        SpeakText(lines[rng.Next(lines.Length)]);
    }

    private void SpeakText(string text)
    {
        if (!voiceCheckBox.Checked) return;
        try
        {
            _ = Task.Run(() =>
            {
                try
                {
                    Type? voiceType = Type.GetTypeFromProgID("SAPI.SpVoice");
                    if (voiceType is null) return;
                    dynamic voice = Activator.CreateInstance(voiceType)!;
                    voice.Rate = 1;
                    voice.Volume = 100;
                    voice.Speak(text, 1);
                }
                catch
                {
                    // Windows SAPI is unavailable on some machines. The game still works without voice.
                }
            });
        }
        catch
        {
            // Ignore speech errors to keep the game stable.
        }
    }

    private void MainForm_KeyDown(object? sender, KeyEventArgs e)
    {
        if (e.Control && e.KeyCode == Keys.Z)
        {
            UndoMove();
            e.Handled = true;
        }
        else if (e.KeyCode == Keys.N)
        {
            ResetGame();
            e.Handled = true;
        }
        else if (e.KeyCode == Keys.Space)
        {
            TogglePause();
            e.Handled = true;
        }
        else if (e.KeyCode == Keys.H)
        {
            ShowHint();
            e.Handled = true;
        }
    }

    private static bool Inside(int r, int c) => r >= 0 && r < 8 && c >= 0 && c < 8;
    private static Side Opposite(Side s) => s == Side.White ? Side.Black : Side.White;
    private static string SideName(Side s) => s == Side.White ? "白方" : "黑方";
    private static string KindLetter(Kind k) => k switch
    {
        Kind.King => "K",
        Kind.Queen => "Q",
        Kind.Rook => "R",
        Kind.Bishop => "B",
        Kind.Knight => "N",
        Kind.Pawn => "",
        _ => ""
    };

    private static string CellName(int row, int col) => $"{(char)('a' + col)}{8 - row}";

    private Rectangle CellRect(int row, int col) => new(boardLeft + col * tile, boardTop + row * tile, tile, tile);

    private bool PointToCell(Point p, out int row, out int col)
    {
        col = (p.X - boardLeft) / tile;
        row = (p.Y - boardTop) / tile;
        return p.X >= boardLeft && p.X < boardLeft + boardPixels && p.Y >= boardTop && p.Y < boardTop + boardPixels;
    }

    private void SetStatus(string text)
    {
        statusText = text;
        statusLabel.Text = text;
        UpdateClockLabels();
    }

    private void PlaySound(string name)
    {
        try
        {
            string file = Path.Combine(AppContext.BaseDirectory, "Resources", "Sounds", name + ".wav");
            if (File.Exists(file)) new SoundPlayer(file).Play();
            else SystemSounds.Asterisk.Play();
        }
        catch
        {
            SystemSounds.Asterisk.Play();
        }
    }

    protected override void Dispose(bool disposing)
    {
        if (disposing)
        {
            foreach (Image img in pieceImages.Values) img.Dispose();
            animationTimer.Dispose();
            clockTimer.Dispose();
            botTimer.Dispose();
            checkmateTimer.Dispose();
        }
        base.Dispose(disposing);
    }
}
