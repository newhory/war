- Entities 사용 테스트를 위한 프로젝트입니다.
- 최적화나 속도 뿐 아니라 실제 프로젝트에 적용했을때 입력, 이벤트, UI등과 연동 구현 테스트용도 입니다.

- 현황
  - 30프레임 이상 안정적인 유닛 수
    - Web 플랫폼 : 유닛수 500~600
    - Windows 플랫폼 : 유닛수 1000~1200
  - 병목 지점
    - Animation, NavMeshAgent 사용을 위한 GameObject와 동기화 시스템
    - 절대적인 Skinning Animation 개체 수
    - 절대적인 NavMeshAgent 개체 수
  - Web 플랫폼 대응을 위한 Hybrid Render 적용 : Entities.Graphics가 지원되는 플랫폼의 경우 사용하지 않음
  - Navigation.AI : NavMesh 사용중. 현재 커스텀 구현 테스트 중.
  - Animation : ECS를 지원하는 Asset 적용 예정
  
