import { useRef, type RefObject } from 'react'
import { useFrame } from '@react-three/fiber'
import { MathUtils, Vector3, type Group } from 'three'

// --------------------------------------------------
// TYPES
// --------------------------------------------------

interface ProductionWorkpieceProps {
  activeStage: number
  pickPlaceTimeRef: RefObject<number>
  gripperRef: RefObject<Group | null>
}

// --------------------------------------------------
// PRODUCTION STATION POSITIONS
// --------------------------------------------------

const stagePositions: Record<
  number,
  readonly [number, number, number]
> = {
  0: [-4.3, 0.895, 0],   // Waiting
  1: [-2.7, 0.895, 0], // End of conveyor belt
  2: [-1.2, 0.575, 0],   // Scanner Station
  3: [1.2, 0.525, 0],    // Hydraulic Press
  4: [3.5, 0.605, 1.05], // Robot pickup station
  5: [4.7, 0.605, 1.05], // Robot output station
}

// --------------------------------------------------
// PRODUCTION WORKPIECE
// --------------------------------------------------

export default function ProductionWorkpiece({
  activeStage,
  pickPlaceTimeRef,
  gripperRef,
}: ProductionWorkpieceProps) {
  const workpieceRef = useRef<Group>(null)
  const gripperWorldPosition = useRef(new Vector3())

  useFrame((_, delta) => {
    const workpiece = workpieceRef.current
    if (!workpiece) return

    const pickTime = pickPlaceTimeRef.current

    // While the robot is holding the box, use the actual
    // world position of its gripper instead of a guessed path.
    if (
      activeStage === 4 &&
      pickTime >= 1.2 &&
      pickTime < 2.8 &&
      gripperRef.current
    ) {
      gripperRef.current.updateWorldMatrix(true, false)
      gripperRef.current.getWorldPosition(
        gripperWorldPosition.current,
      )
      workpiece.position.copy(gripperWorldPosition.current)
      return
    }

    // Before gripping, move onto the pickup platform.
    // After releasing, settle onto the output platform.
    const target =
      activeStage === 4 && pickTime >= 2.8
        ? stagePositions[5]
        : stagePositions[activeStage]

    if (!target) return

    // The completed part settles quickly onto the output tray.
    const movementSpeed = activeStage >= 4 ? 9 : 3

    workpiece.position.x = MathUtils.damp(
      workpiece.position.x,
      target[0],
      movementSpeed,
      delta,
    )

    workpiece.position.y = MathUtils.damp(
      workpiece.position.y,
      target[1],
      movementSpeed,
      delta,
    )

    workpiece.position.z = MathUtils.damp(
      workpiece.position.z,
      target[2],
      movementSpeed,
      delta,
    )
  })

  return (
    <group
      ref={workpieceRef}
      position={[-4.3, 0.895, 0]}
    >
      {/* Main workpiece */}
      <mesh castShadow>
        <boxGeometry args={[0.36, 0.25, 0.36]} />
        <meshStandardMaterial
          color="#d6a66c"
          roughness={0.7}
          metalness={0.1}
        />
      </mesh>

      {/* Top marking */}
      <mesh position={[0, 0.127, 0]}>
        <boxGeometry args={[0.2, 0.008, 0.2]} />
        <meshStandardMaterial
          color="#f8dfad"
          roughness={0.6}
        />
      </mesh>
    </group>
  )
}
