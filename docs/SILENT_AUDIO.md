# 새 실험의 청각 기준

Footsteps.wav와 Door.wav는 작업 시작 전에 존재한 무료 음원 통합 파일을 그대로 참조합니다. 원본 CC0 출처·다운로드 및 가공 기록은 [음원 manifest](../GCCD_Project/Assets/_Project/SilentResearch/Audio/audio-manifest.json), 같은 폴더 Licenses, 보관된 기존 음원 가공 기록를 따릅니다. 파일을 새로운 녹음이나 저자의 창작물로 표시하지 않습니다.

Growl.wav는 `Tools/prepare_silent_growl.py`로 생성한 오리지널 합성 효과음입니다. 실제 사람 목소리, 인물 샘플, 상용 공포게임 녹음, 외부 음원 또는 AI 음원 API를 사용하지 않았습니다. 고정 시드로 동일한 mono/44.1kHz/16-bit, 0.7초 파형을 재생성합니다. 기존 음원의 비침묵 데이터와 새 합성음의 비침묵 데이터를 Unity 생성기에서 검사합니다.

Unity 공간 AudioSource는 사건의 실제 위치를 사용하고 Doppler/loop/playOnAwake를 끕니다. 노출 상한은 모든 유형에서 1초입니다. 세 음원의 지각 음량·유형 의미·방향 식별은 연구자가 파일럿으로 확인해야 합니다. 파형이 존재한다는 검증만으로 청각 기준의 타당성이 보장되지는 않습니다.
