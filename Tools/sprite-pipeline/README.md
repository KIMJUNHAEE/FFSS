# 적 스프라이트 시트 처리 파이프라인

Gamelab Studio(gamelabstudio.co)에서 뽑은 스프라이트 시트를 유니티에 넣기 전에 처리하는 스크립트 모음.

## Gamelab Studio 권장 내보내기 설정
- Frame Dimensions: 1920 x 1920
- Columns: 5 (Rows: Auto)
- Transparent Background: 켜짐
- Enforce Pixel Art: **꺼짐** (켜면 도트/블록 느낌으로 나와서 일러스트풍이 안 됨)
- Padding: 0

## 사용 순서
1. 다운받은 시트를 `Assets/Enemy/{id}/{id}_Idle.png` (또는 `_NomalAttack`, `_Hurt`, `_Death`) 위치에 넣는다.
2. `python grid_slice.py` 안의 `grid_slice("18_Idle", cell_size=1920, cols=5, rows=...)` 처럼 호출해서
   1920x1920 그리드로 잘라 `Assets/Enemy/{id}/{id}_Idle/` 폴더에 개별 프레임 PNG로 저장.
   완전히 비어있는(투명) 칸은 자동으로 건너뜀.
3. 결과가 노이즈 있거나 흐릿하면 `denoise_death.py` 패턴 참고해서 축소 후 `upscale_frames.py`의
   `upscale()`(Real-ESRGAN anime 6B, 4배)로 재복원.
4. 여러 애니메이션 간 캐릭터 크기가 다르게 보이면(정사각형 셀 안에서 자세마다 여백이 달라서)
   `shared_crop.py`로 여러 시트에 공통 크롭을 적용할 수 있음 — 단, 최근에는 **아예 자르지 않고
   1920x1920 그대로 쓰는 쪽으로 결정**했음(사용자 지시, 어떤 애니메이션을 추가해도 캔버스가
   항상 동일해서 크기 문제가 생기지 않음). `EnemyPortrait`가 작아 보이면 스프라이트가 아니라
   유니티 씬의 `EnemyPortrait` Rect 크기를 키울 것.
5. `RealESRGAN_x4plus_anime_6B.pth`는 gitignore 처리됨 — 없으면 아래로 재다운로드:
   `https://github.com/xinntao/Real-ESRGAN/releases/download/v0.2.2.4/RealESRGAN_x4plus_anime_6B.pth`

## 유니티 쪽 마무리
스크립트로 프레임을 만든 뒤에는 유니티 에디터에서
`Card Battle → Setup → Run All` (또는 `3a/3b/3c. Build Battle Scene (38/18/13)`)를 실행해야
씬에 실제로 반영됨.
