# Project War

## 설계

## Component

### Common
- Team
    - TeamColor
- Alive : IEnableableComponent
- MoveToDestination
    - Destination
---
### Troop
- Troop : ISharedComponentData
    - int Id
- TroopEntity
    - TargetSearchRadius

    #### State
    - TroopEngaging : IEnableableComponent
        - NativeArray<Entity> Targets(Troop)        
    - TroopMoving : IEnableableComponent
        - Speed
    - TroopDead : IEnableableComponent
        - Duration
---
### Soldier
- Soldier
    - int Id
- SoldierAnimation
    - Current
    - Next
- SoldierPresentation(Transfrom, Animator)

    #### State
    - SoldierEngaging : IEnableableComponent
        - NativeArray<Entity> Targets(Soldierp)
    - SoldierAttack : IEnableableComponent
        - Entity Target(Soldier)
        - Power
        - Range
        - Duration        
    - SoldierMove : IEnableableComponent
        - Speed
    - SoldierDead : IEnableableComponent
        - Duration    
---
### Formation
- Formation
    - int HorizontalUnitCount
    - int VerticalUnitCount
- FormationUnit
    - Entity Formation
    - int2 PositionInFormation
    - float Radius    
---
## System

### Formation



### Move
- MoveToDestination : Movable
    - Calc Velocity by Destination -> Calc Forward by Velocity
### Boid
### Common
- Gravity
    - apply Gravity to Acc
- Forward
    - lerp forward of LocalTransform
- UpdatePosition : Movable
    - update Velocity with Acc -> update position of LocalTransform


