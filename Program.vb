Option Strict On
Option Infer On
Imports VbPixelGameEngine

' Enumeration of block types corresponding to cell structures in the game
Friend Enum BlockType
    Nucleus = 0 ' Nucleus (2x2, core target block to move to exit)
    ER = 1      ' Endoplasmic Reticulum (1x2, vertical rectangular obstacle)
    Golgi = 2   ' Golgi Apparatus (2x1, horizontal rectangular obstacle)
    Mito = 3    ' Mitochondria (1x1, small square obstacle)
End Enum

' Extension module to get corresponding sprites for each block type
Friend Module BlockTypeExtensions
    <Runtime.CompilerServices.Extension>
    Public Function GetBlockSprite(blockType As BlockType, selected As Boolean) As Sprite
        ' File naming rule: "Assets/[blockType]_[state].png" ("_selected" suffix when selected)
        If blockType = BlockType.ER Then
            Return New Sprite(If(selected, "Assets/ER_selected.png", "Assets/ER.png"))
        Else
            Dim baseName As String = blockType.ToString().ToLower()
            Dim sprPath As String = If(selected,
                $"Assets/{baseName}_selected.png",
                $"Assets/{baseName}.png"
            )
            Return New Sprite(sprPath)
        End If
    End Function
End Module

' Main game program class, inherits from PixelGameEngine
Public NotInheritable Class Program
    Inherits PixelGameEngine

    ' Core game configuration constants and variables
    Private Const GAME_OFFSET_X As Integer = 10
    Private Const GAME_OFFSET_Y As Integer = 10 
    Private Const TILE_SIZE As Integer = 75   
    Private ReadOnly boardScale As New Vi2d(4, 5)
    Private ReadOnly exitPos As New Vi2d(1, 3)   
    Private timeTaken As Single                  
    Private currGameState As GameState          
    Private hasFirstMove As Boolean             

    ' Data structure for game blocks (stores block type and its position on the board)
    Private Class Block
        Public Property Type As BlockType      
        Public Property Position As Vi2d       

        Public Sub New(type As BlockType, x As Integer, y As Integer)
            Me.Type = type
            Position = New Vi2d(x, y)
        End Sub
    End Class

    ' Enumeration of possible game states (controls input, timer, and rendering logic)
    Private Enum GameState
        Playing   ' Game active: input allowed, timer running
        Paused    ' Game paused: no input, timer stopped, selected block cleared
        Finished  ' Game completed: no input, timer stopped
    End Enum

    ' Core game objects
    Private ReadOnly blocks As New List(Of Block) ' Collection of all blocks in the game
    Private selectedBlock As Block = Nothing      ' Currently selected block
    Private ReadOnly blockSizes As New Dictionary(Of BlockType, Vi2d) From {
        {BlockType.Nucleus, New Vi2d(2, 2)},  ' Nucleus: 2 columns x 2 rows (large square)
        {BlockType.ER, New Vi2d(1, 2)},       ' ER: 1 column x 2 rows (vertical rectangle)
        {BlockType.Golgi, New Vi2d(2, 1)},    ' Golgi: 2 columns x 1 row (horizontal rectangle)
        {BlockType.Mito, New Vi2d(1, 1)}      ' Mitochondria: 1 column x 1 row (small square)
    }

    Public Sub New()
        AppName = "BioTech Block Escape"
    End Sub

    Friend Shared Sub Main()
        With New Program
            ' Create game window (800x600 pixels, fullscreen enabled)
            If .Construct(800, 600, fullScreen:=True) Then .Start()
        End With
    End Sub

    Protected Overrides Function OnUserCreate() As Boolean
        SetPixelMode(Pixel.Mode.Mask)
        currGameState = GameState.Playing
        timeTaken = 0 
        hasFirstMove = False
        InitBlocks()
        Return True 
    End Function

    Protected Overrides Function OnUserUpdate(elapsedTime As Single) As Boolean
        Clear(Presets.Mint)

        ' Handle logic based on current game state
        Select Case currGameState
            Case GameState.Playing
                HandleMouseInput()
                HandleKeyInput()   
                If hasFirstMove Then timeTaken += elapsedTime
                CheckWinCondition()

                If GetKey(Key.P).Pressed Then
                    currGameState = GameState.Paused
                    selectedBlock = Nothing
                ElseIf GetKey(Key.R).Pressed Then
                    Call OnUserCreate()
                End If
            Case GameState.Paused
                If GetKey(Key.P).Pressed Then
                    currGameState = GameState.Playing
                ElseIf GetKey(Key.R).Pressed Then
                    Call OnUserCreate()
                End If
            Case GameState.Finished
                If GetKey(Key.R).Pressed Then Call OnUserCreate()
        End Select

        DrawGameElements()  ' Draw all game elements (board, blocks, UI)
        Return Not GetKey(Key.ESCAPE).Pressed  ' Exit the game if 'ESC' is pressed
    End Function

    ' Initialize the layout of blocks (classic Klotski pattern with cell structure theme)
    Private Sub InitBlocks()
        blocks.Clear()  ' Clear existing blocks to avoid duplication

        ' Add blocks with specified type and initial position (X=column, Y=row)
        blocks.Add(New Block(BlockType.Nucleus, 1, 0))
        blocks.Add(New Block(BlockType.ER, 0, 0))
        blocks.Add(New Block(BlockType.ER, 3, 0))
        blocks.Add(New Block(BlockType.ER, 0, 2))
        blocks.Add(New Block(BlockType.ER, 3, 2))
        blocks.Add(New Block(BlockType.Golgi, 1, 2))
        blocks.Add(New Block(BlockType.Mito, 0, 4))
        blocks.Add(New Block(BlockType.Mito, 1, 3))
        blocks.Add(New Block(BlockType.Mito, 2, 3))
        blocks.Add(New Block(BlockType.Mito, 3, 4))
    End Sub

    ' Handle mouse input: Select a block only when left mouse button is clicked
    Private Sub HandleMouseInput()
        If Not GetMouse(0).Pressed Then Exit Sub

        ' Calculate grid coordinates from mouse position (adjusted for game area offset)
        Dim gridX As Integer = (GetMouseX - GAME_OFFSET_X) \ TILE_SIZE
            Dim gridY As Integer = (GetMouseY - GAME_OFFSET_Y) \ TILE_SIZE

        ' Check if the click is within the board boundaries
        If gridX >= 0 AndAlso gridX < boardScale.x AndAlso
           gridY >= 0 AndAlso gridY < boardScale.y Then
            ' Get the block at the clicked grid position
            selectedBlock = BlockAt(gridX, gridY)

            ' Mark first move as made if this is the first interaction
            If Not hasFirstMove Then hasFirstMove = True
        End If
    End Sub

    ' Handle keyboard input: Move the selected block using arrow keys
    Private Sub HandleKeyInput()
        If selectedBlock Is Nothing Then Exit Sub ' Exit if no block is selected

        ' Calculate movement direction (dx = horizontal offset, dy = vertical offset)
        Dim dx As Integer = 0, dy As Integer = 0
        Select Case True
            Case GetKey(Key.UP).Pressed
                dy = -1    ' Move up (decrease row Y)
            Case GetKey(Key.DOWN).Pressed
                dy = 1     ' Move down (increase row Y)
            Case GetKey(Key.LEFT).Pressed
                dx = -1    ' Move left (decrease column X)
            Case GetKey(Key.RIGHT).Pressed
                dx = 1     ' Move right (increase column X)
        End Select

        ' Check if movement is valid, then update block position
        If dx <> 0 OrElse dy <> 0 Then
            If IsValidMove(selectedBlock, dx, dy) Then
                selectedBlock.Position += New Vi2d(dx, dy)

                ' Mark first move as made if this is the first interaction
                If Not hasFirstMove Then hasFirstMove = True
            End If
        End If
    End Sub

    ' Draw all game elements: board, exit, blocks, and UI text
    Private Sub DrawGameElements()
        ' Draw board background (grid lines with game area offset)
        For y As Integer = 0 To boardScale.y - 1
            For x As Integer = 0 To boardScale.x - 1
                ' Calculate draw position (grid position × tile size + offset)
                Dim drawX As Integer = x * TILE_SIZE + GAME_OFFSET_X
                Dim drawY As Integer = y * TILE_SIZE + GAME_OFFSET_Y
                ' Draw grid cell (gray border for clarity)
                DrawRect(drawX, drawY, TILE_SIZE, TILE_SIZE, Presets.Gray)
            Next x
        Next y

        ' Draw exit marker (red rectangle to indicate target position)
        Dim exitDrawX As Integer = exitPos.x * TILE_SIZE + GAME_OFFSET_X
        Dim exitDrawY As Integer = exitPos.y * TILE_SIZE + GAME_OFFSET_Y
        Dim exitDrawSize As Integer = blockSizes(BlockType.Nucleus).x * TILE_SIZE
        DrawRect(exitDrawX, exitDrawY, exitDrawSize, exitDrawSize, Presets.Red)
        ' Draw exit text (positioned at bottom of exit marker)
        DrawString(exitDrawX + 25, exitDrawY + exitDrawSize - 10, "* EXIT HERE *", Presets.Red)

        ' Draw all blocks (with selected state sprite)
        For Each block As Block In blocks
            ' Get corresponding sprite (selected or normal state)
            Dim spr As Sprite = block.Type.GetBlockSprite(block Is selectedBlock)
            ' Calculate block draw position (adjusted for game area offset)
            Dim drawX As Integer = block.Position.x * TILE_SIZE + GAME_OFFSET_X
            Dim drawY As Integer = block.Position.y * TILE_SIZE + GAME_OFFSET_Y
            ' Draw block sprite (scaled to match tile size)
            DrawSprite(drawX, drawY, spr, 5)
        Next block

        ' Draw UI text (timer and state prompts, aligned with game area)
        Dim uiY As Integer = boardScale.y * TILE_SIZE + GAME_OFFSET_Y + 10
        ' Draw elapsed time (formatted to 2 decimal places)
        DrawString(GAME_OFFSET_X, uiY, $"TIME TAKEN: {timeTaken:F2} sec", Presets.Black, 2)

        ' Draw state-specific prompts (adjusted for game state)
        Select Case currGameState
            Case GameState.Playing
                DrawString(GAME_OFFSET_X, uiY + 30,
                           "Click to select a block; move it with arrow keys",
                           Presets.DarkBlue, 2)
                DrawString(GAME_OFFSET_X, uiY + 55,
                           "'P' to pause, 'R' to restart, 'ESC' to exit",
                           Presets.DarkBlue, 2)
            Case GameState.Paused
                DrawString(GAME_OFFSET_X, uiY + 30,
                           "GAME PAUSED - 'P' to resume, 'R' to restart",
                           Presets.DarkRed, 2)
            Case GameState.Finished
                DrawString(GAME_OFFSET_X, uiY + 30,
                           "GAME COMPLETED! 'R' to restart, 'ESC' to exit",
                           Presets.DarkGreen, 2)
        End Select

        ' Draw game title sprite (positioned at top-right area)
        DrawSprite(325, 50, New Sprite("Assets/title_card.png"), 3)
        ' Draw game introduction text (aligned with title sprite)
        DrawString(320, 192, "Welcome to BioTech Puzzle!", Presets.Olive, 2)
        DrawString(320, 222, "Cell parts = movable blocks.", Presets.Olive, 2)
        DrawString(320, 252, "Guide Nucleus to the exit.", Presets.Olive, 2)
        DrawString(320, 282, "Test your skills right now!", Presets.Olive, 2)
    End Sub

    ' Check if the game is won (Nucleus has reached the exit position)
    Private Sub CheckWinCondition()
        Dim nucleus As Block =  ' Find the Nucleus block in the blocks collection
            Aggregate b In blocks Where b.Type = BlockType.Nucleus Into FirstOrDefault()

        ' Exit if no Nucleus is found or game is not in "Playing" state
        If nucleus Is Nothing OrElse currGameState <> GameState.Playing Then Exit Sub
        ' Win condition: Nucleus position exactly matches the exit position
        If nucleus.Position = exitPos Then
            currGameState = GameState.Finished
            selectedBlock = Nothing  ' Clear selected block when game ends
        End If
    End Sub

    ' Check if a block movement is valid (no boundary violation or block overlap)
    Private Function IsValidMove(block As Block, dx As Integer, dy As Integer) As Boolean
        ' Calculate new position after movement, and get size of the block being moved.
        Dim newPos As New Vi2d(block.Position.x + dx, block.Position.y + dy)
        Dim blockSize = blockSizes(block.Type)

        ' Boundary check: Ensure block stays within the board
        If newPos.x < 0 OrElse newPos.y < 0 OrElse
           newPos.x + blockSize.x > boardScale.x OrElse
           newPos.y + blockSize.y > boardScale.y Then Return False

        ' Collision check: Ensure block does not overlap with other blocks
        For Each other As Block In blocks
            If other Is block Then Continue For ' Skip checking against itself
            
            ' AABB collision detection (Axis-Aligned Bounding Box)
            Dim otherSize = blockSizes(other.Type)
            If Not (newPos.x + blockSize.x <= other.Position.x OrElse
                    newPos.x >= other.Position.x + otherSize.x OrElse
                    newPos.y + blockSize.y <= other.Position.y OrElse
                    newPos.y >= other.Position.y + otherSize.y) Then Return False
        Next other

        Return True
    End Function

    ' Get the block located at the specified grid coordinates (X=column, Y=row)
    Private ReadOnly Property BlockAt(gridX As Integer, gridY As Integer) As Block
        Get
            For Each block As Block In blocks
                Dim blockSize = blockSizes(block.Type)
                ' Check if the grid coordinates are within the block's area;
                ' return the block if coordinates match
                If gridX >= block.Position.x AndAlso
                   gridX < block.Position.x + blockSize.x AndAlso
                   gridY >= block.Position.y AndAlso
                   gridY < block.Position.y + blockSize.y Then Return block 
            Next block
            Return Nothing
        End Get
    End Property
End Class