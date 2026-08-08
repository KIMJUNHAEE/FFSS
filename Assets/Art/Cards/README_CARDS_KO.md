# 상위 포커·상대 전용 섯다 카드 팩

- `Assets/Resources/Cards/AscendantPoker`: 상위 포커 52장, 적/흑 조커 2장, 뒷면 1장
- `Assets/Resources/Cards/SignatureSeotda`: 상대 17명 전용 섯다 카드
- `ascendant_poker_catalog.csv`: 포커 카드별 등급과 효과
- `signature_seotda_catalog.csv`: 상대별 월, 광 여부, 발동 효과
- 포커 조커는 `PokerHandEvaluator`가 적 조커는 하트/다이아, 흑 조커는 스페이드/클로버의 미사용 카드 한 장으로만 대체해 최선의 족보를 선택한다.
- 상대 전용 섯다 카드는 `OpponentSeotdaCardCatalog`의 조건과 `RpsCombatController`의 전투 보너스로 연결된다.
