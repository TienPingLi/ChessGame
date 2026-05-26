# Chess Game

<img width="984" height="747" alt="image" src="https://github.com/user-attachments/assets/43fe71a7-6167-4357-9b31-4b14bcd40bbe" />


這是一個以西洋棋為主題的 C# WinForms 棋牌類遊戲專案，風格參考線上棋盤網站的乾淨棋盤、合法步提示、移動動畫與音效回饋。專案使用 PNG 棋子圖片與 WAV 音效，並加入可調整視窗大小的棋盤、悔棋、計時器、簡易電腦對手與嘲諷語音。

## 功能特色

- 8 x 8 西洋棋棋盤，棋盤會隨視窗大小自動縮放
- 白方與黑方輪流下棋
- 點選棋子後顯示合法移動位置
- 綠點代表可移動位置，紅圈代表可吃子位置
- 棋子滑動動畫
- 移動、吃子、選取、將軍、遊戲結束音效
- 最後一步淡黃色標示
- 將軍、將死、無合法步和棋判斷
- 強化將死動畫：暗場、黃金攻擊箭頭、王位紅色脈衝、火花粒子與勝利橫幅
- 兵到底線自動升變皇后
- 悔棋功能
- 3 / 5 / 10 / 15 分鐘計時器
- 暫停 / 繼續功能
- 簡易人機對戰：玩家執白棋，電腦執黑棋
- 不使用機器學習，電腦以棋子價值、吃子、中心控制與一層回應評估選步
- 提示功能，會建議目前回合可走的一步
- 右側顯示走棋紀錄與吃子統計
- 嘲諷語音：使用 Windows 內建 SAPI 語音合成，沒有額外套件也能執行

## 操作方式

1. 點選自己的棋子。
2. 棋盤會顯示該棋子的合法移動位置。
3. 點綠點可移動，點紅圈可吃子。
4. 勾選「人機對戰」後，玩家控制白棋，電腦控制黑棋。
5. 勾選「開啟嘲諷語音」後，吃子、將軍、將死等情況會播放語音。
6. 將死時會顯示 Checkmate 勝利動畫；可按 `N` 重新開始或 `Ctrl + Z` 悔棋。

## 快捷鍵

| 快捷鍵 | 功能 |
|---|---|
| Ctrl + Z | 悔棋 |
| N | 新遊戲 |
| Space | 暫停 / 繼續 |
| H | 顯示提示 |

## 執行方式

1. 使用 Visual Studio 2022 開啟 `AnimatedChessGame.csproj`。
2. 確認已安裝 `.NET Desktop Development` 工作負載。
3. 按 `F5` 執行。

## 專案資料夾結構

```text
AnimatedChessGame/
├─ AnimatedChessGame.csproj
├─ Program.cs
├─ MainForm.cs
├─ MainForm.Designer.cs
├─ README.md
├─ .gitignore
└─ Resources/
   ├─ Pieces/
   │  └─ 12 個西洋棋棋子 PNG 圖片
   └─ Sounds/
      ├─ move.wav
      ├─ capture.wav
      ├─ select.wav
      ├─ check.wav
      └─ gameover.wav
```

## 已知限制

本專案已實作基本完整的棋子移動、合法步、將軍與將死判斷，但尚未加入以下進階規則：

- 王車易位 Castling
- 吃過路兵 En Passant
- 手動選擇升變棋子，目前預設升變為皇后

## GitHub 上傳注意事項

上傳 GitHub 前請不要上傳以下資料夾：

```text
bin/
obj/
.vs/
.git/
```

本專案已附上 `.gitignore`，可以降低誤傳編譯檔案的機率。

## 素材說明

- 棋子圖片放在 `Resources/Pieces/`
- 音效放在 `Resources/Sounds/`
- 嘲諷語音不是固定音檔，而是用 Windows 內建語音合成動態播放

若想改成自己錄製或 TTSMaker 產生的語音，可將語音檔放進 `Resources/Sounds/`，再在 `MainForm.cs` 裡面的 `SpeakTaunt` 或 `PlaySound` 函式加入對應檔名。
