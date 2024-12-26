![image](https://github.com/user-attachments/assets/79f602cb-c05e-44e7-a3e6-1721c33fa423)

Mini Bone Tool 사용자 메뉴얼

Mesh에 간단한 리깅 작업을 할 수 있는 유니티 플러그인입니다.
Mini Bone Tool 사용 방법을 안내합니다.



사용 방법


1. 메뉴 열기
에디터 상단 메뉴에서 Tools > Mini Bone Tool을 선택합니다.
→ Mini Bone Tool UI가 나타납니다.


2. 대상 오브젝트 설정
Mini Bone Tool UI의 "대상 오브젝트" 필드에 리깅할 3D 오브젝트(예: Cube, Plane, Quad, Sphere)를 드래그 앤 드롭합니다.


3. Root Bone 추가
"Root Bone 추가" 버튼을 클릭합니다.
→ 대상 오브젝트에 Root 오브젝트가 생성됩니다.


4. Bone 생성
"새로운 Bone 생성" 버튼을 눌러 Bone을 추가합니다.
→ 생성된 Bone은 Scene 뷰에서 확인할 수 있습니다.


5. Bone 위치 및 속성 조정
Bone 오브젝트의 위치를 조정합니다.
"Widget 범위"","Widget 강도" 값을 변경하여 Bone의 영향을 시각적으로 확인합니다.
필요하면 "새로운 Bone 생성" 버튼을 반복 클릭해 추가 Bone을 생성합니다.


6. 리깅 적용
"작업 시작" 버튼을 클릭하여 리깅을 적용합니다.
→ 대상 오브젝트의 Mesh에 Bone 리깅이 완료됩니다.
리깅 데이터가 생성되는 새로운 Mesh는 'MiniBoneTool/Meshs' 경로에 저장이 됩니다.
Mesh에 리깅 데이터가 있으면 기존 데이터에 덮어 씌워집니다.


7. 컴포넌트 정리
리깅 작업 후 "컴포넌트 정리" 버튼을 눌러 Gizmo 관련 컴포넌트를 정리합니다.

참고: UI 창을 닫으면 컴포넌트가 자동으로 정리됩니다.



- 추가 팁
작업 시작 버튼을 누르면 Bone 목록 정보들이 Root 오브젝트에 저장이 됩니다.
'저장된 데이터 불러오기' 버튼으로 데이터를 불러올 수 있습니다.
