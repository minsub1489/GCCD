# X40 방향 매핑과 시작 전 검사

기본 출력은 Player의 VestFront(201), VestBack(202)에 각각 20개를 보내는 X40 경로입니다. 한 메시지에 앞·뒤 프레임을 함께 넣습니다. 32개 배열로 줄이지 않고 19/20 경계와 32~39 채널도 유지합니다. SDK2 PlayMotors에 40배열을 넣는 대체 경로는 사용하지 않습니다.

이전 미션은 전송만 40개였고 방위각은 9도 간격의 가상 원형 맵이었습니다. 새 기본 맵은 앞/뒤 각각 4열×5행이며 같은 열의 세로 모터는 같은 방위각을 사용합니다. 0도는 가슴 앞, 90도는 착용자 오른쪽, 180도는 등, 270도는 착용자 왼쪽입니다.

## 인덱스 근거와 한계

공식 구형 [VisualFeedback.cs](https://github.com/bhaptics/haptic-library/blob/master/samples/unity-plugin/Assets/Bhaptics/SDK/Scripts/VisualFeedback.cs)의 자식 순서와 [시각화 프리팹](https://github.com/bhaptics/haptic-library/blob/master/samples/unity-plugin/Assets/Bhaptics/SDK/Prefabs/%5BbHapticsVisualizer%5D.prefab)을 대조했습니다. 앞면 그림에서는 인덱스 0이 오른쪽, 뒷면 그림에서는 0이 왼쪽에 배치되어 모두 착용자 왼쪽 열로 해석합니다. 프로토콜의 앞/뒤 ID와 패널당 인덱스 0~19는 공식 [model.h](https://github.com/bhaptics/haptic-library/blob/master/include/shared/model.h)에 근거합니다.

수평 방위각은 8개 열의 몸통 둘레 모델이며 실제 착용자 신체에서 측정한 각도는 아닙니다. 앞 열은 -67.5/-22.5/+22.5/+67.5도, 뒤 열은 247.5/202.5/157.5/112.5도로 모델링했습니다. 실제 체감은 조끼를 입고 확인해야 합니다. 기본 템플릿은 PhysicalLayoutVerified와 개별 PlacementConfirmed를 자동으로 설정하지 않습니다.

## 간단한 확인

1. 로비에서 **짧은 진동 · 방향 확인**을 엽니다.
2. 조끼를 입고 화면 정면을 향해 앉습니다. **앞 / 오른쪽 / 뒤 / 왼쪽** 중 하나를 누르면 0.2초 요청을 보냅니다.
3. 해당 방향에서 실제로 느꼈을 때만 **방금 누른 방향에서 느껴졌어요**를 누릅니다. 직접 확인한 방향 수를 표시합니다.
4. 반대로 느껴지면 접힌 **방향이 반대로 느껴지면 보정**을 열어 앞면 좌우 / 뒷면 좌우 / 앞뒤 교환을 조절하고 다시 검사합니다.
5. **확인 마치기** 후 작전을 시작합니다. 이 확인은 연구 본 측정의 40개 모터 실측 교정을 대신하지 않습니다.

보정은 해당 PC의 `DtsSettings/x40_layout.json`에 저장하고 미션과 방향 검사가 같은 프로필을 사용합니다. 연구자의 잠긴 실측 프로필을 덮어쓰지 않습니다. 미션 이벤트 JSON과 events.csv에 사용한 매핑 버전을 기록합니다. 방향 검사 요청 배열과 출력 receipt는 CALIBRATION/direction-*.json에 보존합니다. 검사 완료 표시는 실제 착용자의 버튼 응답이며 하드웨어 전송 API의 보증이 아닙니다.

## 결과 좌표

지도 입력에서 contentRect의 테두리 오프셋이 누락돼 클릭 위치가 조금 밀리던 문제를 수정했습니다. 지도 클릭 → 저장 좌표 → 결과 표시를 대조하고 UI 배율 0.8에서도 같은 지점으로 돌아오는지 확인했습니다. 실제/응답 좌표를 뒤집지 않는지도 검사했습니다. 이미 저장된 이전 좌표는 원본 그대로 보존합니다.
