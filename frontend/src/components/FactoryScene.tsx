
import { Suspense, useRef, useState, type RefObject } from 'react'
import { Canvas, useFrame } from '@react-three/fiber'
import { OrbitControls, useGLTF } from '@react-three/drei'
import { MathUtils, type Group, type Mesh } from 'three'
import ProductionWorkpiece from './ProductionWorkpiece'

// --------------------------------------------------
// TYPES
// --------------------------------------------------

interface FactoryMachine {
  id: string
  name: string
  type: number
  status: number
}

interface FactorySceneProps {
  machines: FactoryMachine[]
  activeStage?: number | null
  simulationId?: string | null
  simulationStatus?: number | null
}

// --------------------------------------------------
// MACHINE INFORMATION
// --------------------------------------------------

const machineNames = [
  'Conveyor Belt',
  'Scanner Station',
  'Hydraulic Press',
  'Robot Arm',
]

const machineStatuses = [
  'OFFLINE',
  'IDLE',
  'RUNNING',
  'WARNING',
  'ERROR',
]

const machineStatusColors = [
  '#64748b', // OFFLINE
  '#38bdf8', // IDLE
  '#22c55e', // RUNNING
  '#f59e0b', // WARNING
  '#ef4444', // ERROR
]

// --------------------------------------------------
// CONVEYOR PACKAGE ANIMATION
// --------------------------------------------------

interface ConveyorPackageProps {
  running: boolean
}

function ConveyorPackage({ running }: ConveyorPackageProps) {
  const packageRef = useRef<Mesh>(null)

  useFrame((_, delta) => {
    if (!packageRef.current || !running) return

    packageRef.current.position.x += delta * 0.7

    if (packageRef.current.position.x > 0.75) {
      packageRef.current.position.x = -0.75
    }
  })

  return (
    <mesh
      ref={packageRef}
      position={[-0.75, 0.895, 0]}
      castShadow
    >
      <boxGeometry args={[0.32, 0.25, 0.36]} />

      <meshStandardMaterial
        color="#d6a66c"
        roughness={0.8}
      />
    </mesh>
  )
}

// --------------------------------------------------
// HYDRAULIC PRESS ANIMATION
// --------------------------------------------------

interface PressMechanismProps {
  running: boolean
}

function PressMechanism({ running }: PressMechanismProps) {
  const mechanismRef = useRef<Group>(null)
  const cycleRef = useRef(0)

  useFrame((_, delta) => {
    if (!mechanismRef.current || !running) return

    cycleRef.current += delta * 2.4

    const stroke =
      (1 - Math.cos(cycleRef.current)) / 2

    mechanismRef.current.position.y =
      -0.28 * stroke
  })

  return (
    <group ref={mechanismRef}>
      {/* Hydraulic piston */}
      <mesh position={[0, 1.25, 0]} castShadow>
        <cylinderGeometry
          args={[0.18, 0.18, 0.8, 24]}
        />
        <meshStandardMaterial color="#cbd5e1" />
      </mesh>

      {/* Moving press plate */}
      <mesh position={[0, 0.8, 0]} castShadow>
        <boxGeometry args={[0.9, 0.2, 0.8]} />
        <meshStandardMaterial color="#fbbf24" />
      </mesh>
    </group>
  )
}

// --------------------------------------------------
// ROBOT ARM ANIMATION
// --------------------------------------------------

// --------------------------------------------------
// PICK-AND-PLACE TIMELINE (seconds)
// --------------------------------------------------

// These coordinates are world-space positions of the gripper.
const PICKUP_POINT = [3.5, 0.605, 1.05] as const
const OUTPUT_POINT = [4.7, 0.605, 1.05] as const
const TRANSFER_HEIGHT = 1.2

function smoothProgress(time: number, start: number, end: number) {
  const t = MathUtils.clamp((time - start) / (end - start), 0, 1)
  return t * t * (3 - 2 * t)
}

function robotPickPlacePose(time: number) {
  const pickup = PICKUP_POINT
  const output = OUTPUT_POINT

  let x: number = pickup[0]
  let y: number = TRANSFER_HEIGHT
  let z: number = pickup[2]

  if (time < 0.9) {
    // Approach the package from above.
    y = MathUtils.lerp(
      TRANSFER_HEIGHT,
      pickup[1],
      smoothProgress(time, 0.45, 0.9),
    )
  } else if (time < 1.2) {
    // Close the gripper around the package.
    y = pickup[1]
  } else if (time < 1.7) {
    // Lift the package.
    y = MathUtils.lerp(
      pickup[1],
      TRANSFER_HEIGHT,
      smoothProgress(time, 1.2, 1.7),
    )
  } else if (time < 2.3) {
    // Rotate the arm towards the output platform.
    x = MathUtils.lerp(
      pickup[0],
      output[0],
      smoothProgress(time, 1.7, 2.3),
    )
  } else if (time < 2.8) {
    // Lower the package onto the output platform.
    x = output[0]
    y = MathUtils.lerp(
      TRANSFER_HEIGHT,
      output[1],
      smoothProgress(time, 2.3, 2.8),
    )
  } else {
    // Release, then retract the gripper.
    x = output[0]
    y = MathUtils.lerp(
      output[1],
      TRANSFER_HEIGHT,
      smoothProgress(time, 3.1, 3.6),
    )
  }

  // Keep z aligned with the two stations.
  z = MathUtils.lerp(pickup[2], output[2], smoothProgress(time, 1.7, 2.3))

  let grip = 0
  if (time >= 0.9 && time < 1.2) {
    grip = smoothProgress(time, 0.9, 1.2)
  } else if (time >= 1.2 && time < 2.8) {
    grip = 1
  } else if (time >= 2.8 && time < 3.1) {
    grip = 1 - smoothProgress(time, 2.8, 3.1)
  }

  return { x, y, z, grip }
}

// Two-link inverse kinematics for the robot's existing mesh lengths.
function solveArmPose(x: number, y: number, z: number) {
  const localX = x - 3.5 // Robot group is positioned at x=3.5.
  const radius = Math.hypot(localX, z)
  const height = y - 0.95 // Shoulder pivot height.
  const upperArm = 0.9
  const forearm = 0.89

  const cosine = MathUtils.clamp(
    (radius * radius + height * height - upperArm ** 2 - forearm ** 2) /
      (2 * upperArm * forearm),
    -1,
    1,
  )

  // Negative elbow angle bends the forearm downward to the gripper.
  const elbow = -Math.acos(cosine)
  const shoulder =
    Math.atan2(height, radius) -
    Math.atan2(
      forearm * Math.sin(elbow),
      upperArm + forearm * Math.cos(elbow),
    ) -
    Math.PI / 2

  return {
    yaw: -Math.atan2(z, localX),
    shoulder,
    elbow,
  }
}

interface RobotArmMechanismProps {
  running: boolean
  pickPlaceActive: boolean
  pickPlaceTimeRef: RefObject<number>
  gripperRef: RefObject<Group | null>
  visual: {
    color: string
    emissive: string
    emissiveIntensity: number
  }
}

function RobotArmMechanism({
  running,
  pickPlaceActive,
  pickPlaceTimeRef,
  gripperRef,
  visual,
}: RobotArmMechanismProps) {
  const baseRef = useRef<Group>(null)
  const shoulderRef = useRef<Group>(null)
  const elbowRef = useRef<Group>(null)

  // Gripper fingers
  const leftFingerRef = useRef<Mesh>(null)
  const rightFingerRef = useRef<Mesh>(null)

  const cycleRef = useRef(0)

  useFrame((_, delta) => {
    if (pickPlaceActive) {
      pickPlaceTimeRef.current += delta
      const pose = robotPickPlacePose(pickPlaceTimeRef.current)
      // Return to the neutral pose after placing the package.
      const angles = pickPlaceTimeRef.current >= 3.6
        ? { yaw: 0, shoulder: -0.6, elbow: 0.8 }
        : solveArmPose(pose.x, pose.y, pose.z)

      // Smoothly guide the joints to each programmed pickup pose.
      if (baseRef.current) {
        baseRef.current.rotation.y = MathUtils.damp(
          baseRef.current.rotation.y,
          angles.yaw,
          18,
          delta,
        )
      }
      if (shoulderRef.current) {
        shoulderRef.current.rotation.z = MathUtils.damp(
          shoulderRef.current.rotation.z,
          angles.shoulder,
          18,
          delta,
        )
      }
      if (elbowRef.current) {
        elbowRef.current.rotation.z = MathUtils.damp(
          elbowRef.current.rotation.z,
          angles.elbow,
          18,
          delta,
        )
      }

      if (leftFingerRef.current) {
        leftFingerRef.current.position.x = -0.32 + pose.grip * 0.095
      }
      if (rightFingerRef.current) {
        rightFingerRef.current.position.x = 0.32 - pose.grip * 0.095
      }
      return
    }

    // Reset the pick-and-place clock outside robot production.
    pickPlaceTimeRef.current = 0

    // When idle, gently return the entire robot to its home pose.
    if (!running) {
      const homingSpeed = 4
      if (baseRef.current) {
        baseRef.current.rotation.y = MathUtils.damp(
          baseRef.current.rotation.y, 0, homingSpeed, delta,
        )
      }
      if (shoulderRef.current) {
        shoulderRef.current.rotation.z = MathUtils.damp(
          shoulderRef.current.rotation.z, -0.6, homingSpeed, delta,
        )
      }
      if (elbowRef.current) {
        elbowRef.current.rotation.z = MathUtils.damp(
          elbowRef.current.rotation.z, 0.8, homingSpeed, delta,
        )
      }
      if (leftFingerRef.current) {
        leftFingerRef.current.position.x = MathUtils.damp(
          leftFingerRef.current.position.x, -0.32, homingSpeed, delta,
        )
      }
      if (rightFingerRef.current) {
        rightFingerRef.current.position.x = MathUtils.damp(
          rightFingerRef.current.position.x, 0.32, homingSpeed, delta,
        )
      }
      return
    }

    cycleRef.current += delta * 1.4

    const time = cycleRef.current

    // Rotate base
    if (baseRef.current) {
      baseRef.current.rotation.y =
        Math.sin(time * 0.6) * 0.55
    }

    // Move shoulder
    if (shoulderRef.current) {
      shoulderRef.current.rotation.z =
        -0.6 + Math.sin(time) * 0.3
    }

    // Move elbow
    if (elbowRef.current) {
      elbowRef.current.rotation.z =
        0.8 + Math.sin(time + 1.2) * 0.4
    }

    // Open and close the gripper fingers.
    const grip = (Math.sin(time * 1.4) + 1) / 2

    if (leftFingerRef.current) {
      leftFingerRef.current.position.x =
        -0.32 + grip * 0.095
    }

    if (rightFingerRef.current) {
      rightFingerRef.current.position.x =
        0.32 - grip * 0.095
    }
  })

  return (
    <>
      {/* Fixed robot base */}
      <mesh position={[0, 0.18, 0]} castShadow>
        <cylinderGeometry
          args={[0.65, 0.75, 0.36, 32]}
        />
        <meshStandardMaterial color="#475569" />
      </mesh>

      {/* Rotating base */}
      <group ref={baseRef}>
        <mesh position={[0, 0.65, 0]} castShadow>
          <cylinderGeometry
            args={[0.22, 0.3, 0.8, 24]}
          />
          <meshStandardMaterial color="#0ea5e9" />
        </mesh>

        {/* Shoulder joint */}
        <group
          ref={shoulderRef}
          position={[0, 0.95, 0]}
          rotation={[0, 0, -0.6]}
        >
          <mesh position={[0, 0, 0]} castShadow>
            <sphereGeometry args={[0.24, 24, 24]} />
            <meshStandardMaterial color="#64748b" />
          </mesh>

          {/* First arm segment */}
          <mesh position={[0, 0.45, 0]} castShadow>
            <boxGeometry args={[0.3, 0.9, 0.35]} />
            <meshStandardMaterial {...visual} />
          </mesh>

          {/* Elbow joint */}
          <group
            ref={elbowRef}
            position={[0, 0.9, 0]}
            rotation={[0, 0, 0.8]}
          >
            <mesh position={[0, 0, 0]} castShadow>
              <sphereGeometry args={[0.21, 24, 24]} />
              <meshStandardMaterial color="#94a3b8" />
            </mesh>

            {/* Second arm segment */}
            <mesh position={[0, 0.38, 0]} castShadow>
              <boxGeometry args={[0.25, 0.76, 0.3]} />
              <meshStandardMaterial color="#0284c7" />
            </mesh>

            {/* Gripper base */}
            <mesh position={[0, 0.76, 0]} castShadow>
              <boxGeometry args={[0.72, 0.18, 0.35]} />
              <meshStandardMaterial color="#e2e8f0" />
            </mesh>

            {/* Gripper tracking anchor for the workpiece */}
            <group ref={gripperRef} position={[0, 0.89, 0]} />

            {/* Left gripper finger */}
            <mesh
              ref={leftFingerRef}
              position={[-0.32, 0.89, 0]}
              castShadow
            >
              <boxGeometry args={[0.08, 0.22, 0.12]} />
              <meshStandardMaterial color="#94a3b8" />
            </mesh>

            {/* Right gripper finger */}
            <mesh
              ref={rightFingerRef}
              position={[0.32, 0.89, 0]}
              castShadow
            >
              <boxGeometry args={[0.08, 0.22, 0.12]} />
              <meshStandardMaterial color="#94a3b8" />
            </mesh>
          </group>
        </group>
      </group>
    </>
  )
}

// --------------------------------------------------
// SCANNER ANIMATION
// --------------------------------------------------

interface ScannerBeamProps {
  running: boolean
}

function ScannerBeam({ running }: ScannerBeamProps) {
  const beamRef = useRef<Mesh>(null)
  const cycleRef = useRef(0)

  useFrame((_, delta) => {
    if (!beamRef.current || !running) return

    cycleRef.current += delta * 3

    beamRef.current.position.y =
      1.0 + Math.sin(cycleRef.current) * 0.35
  })

  if (!running) return null

  return (
    <mesh
      ref={beamRef}
      position={[0, 1.0, 0]}
    >
      <boxGeometry args={[0.95, 0.045, 0.12]} />

      <meshBasicMaterial
        color="#22d3ee"
        transparent
        opacity={0.9}
        toneMapped={false}
      />
    </mesh>
  )
}

// --------------------------------------------------
// ACTIVE PRODUCTION STAGE MARKER
// --------------------------------------------------

interface ActiveStageMarkerProps {
  active: boolean
}

function ActiveStageMarker({
  active,
}: ActiveStageMarkerProps) {
  if (!active) return null

  return (
    <mesh
      position={[0, 0.05, 0]}
      rotation={[-Math.PI / 2, 0, 0]}
    >
      <ringGeometry args={[0.85, 1.05, 64]} />

      <meshBasicMaterial
        color="#22c55e"
        transparent
        opacity={0.9}
        depthWrite={false}
        toneMapped={false}
      />
    </mesh>
  )
}

// --------------------------------------------------
// FACTORY SCENE
// --------------------------------------------------


function BlenderConveyor() {
  const { scene } = useGLTF('/models/conveyor.glb')

  return <primitive object={scene} />
}


function BlenderScanner() {
  const { scene } = useGLTF('/models/scanner.glb')

  return <primitive object={scene} />
}


function BlenderPress() {
  const { scene } = useGLTF('/models/press.glb')

  return <primitive object={scene} />
}




export default function FactoryScene({
  machines,
  activeStage = null,
  simulationId = null,
  simulationStatus = null,
}: FactorySceneProps) {
  const [selectedType, setSelectedType] =
    useState<number | null>(null)

  // One shared clock and one physical gripper anchor keep the workpiece
  // synchronized with the robot during automatic production.
  const pickPlaceTimeRef = useRef(0)
  const gripperRef = useRef<Group>(null)

  const selectedMachine = machines.find(
    (machine) => machine.type === selectedType,
  )

  // Keep the finished part on the output station after a
  // successfully completed simulation. A new simulation ID
  // resets the workpiece via its React key.
  const productionCompleted =
    simulationId !== null && simulationStatus === 2
  const showProductionWorkpiece =
    activeStage !== null || productionCompleted

  // --------------------------------------------------
  // PRODUCTION STAGE SYNCHRONIZATION
  // --------------------------------------------------

  function isMachineRunning(
    machineType: number,
    productionStage: number,
  ): boolean {
    if (activeStage !== null) {
      return activeStage === productionStage
    }

    return machines.some(
      (machine) =>
        machine.type === machineType &&
        machine.status === 2,
    )
  }

  // --------------------------------------------------
  // MACHINE ANIMATION STATES
  // --------------------------------------------------

  const conveyorRunning = isMachineRunning(0, 1)
  const scannerRunning = isMachineRunning(1, 2)
  const pressRunning = isMachineRunning(2, 3)
  const robotRunning = isMachineRunning(3, 4)

  // --------------------------------------------------
  // LIVE MACHINE VISUALS
  // --------------------------------------------------

  function getMachineVisual(type: number) {
    const machine = machines.find(
      (machine) => machine.type === type,
    )

    const color =
      machineStatusColors[machine?.status ?? 0] ??
      '#64748b'

    const isSelected = selectedType === type

    return {
      color,
      emissive: isSelected ? '#ffffff' : '#000000',
      emissiveIntensity: isSelected ? 0.25 : 0,
    }
  }

  // --------------------------------------------------
  // FACTORY UI
  // --------------------------------------------------

  return (
    <section className="factory-section">
      <h2>3D Factory Overview</h2>

      <p>
        Interactive digital twin of the HELIOS production line.
        Drag to rotate, scroll to zoom.
        Click a machine to view its information.
      </p>

      <div
        style={{
          width: '100%',
          height: '460px',
          borderRadius: '14px',
          overflow: 'hidden',
          border: '1px solid #334155',
          background: '#0b1220',
        }}
      >
        <Canvas
          shadows
          camera={{
            position: [8, 7, 11],
            fov: 45,
          }}
        >
          <color
            attach="background"
            args={['#0b1220']}
          />

          {/* -------------------------------- */}
          {/* LIGHTING */}
          {/* -------------------------------- */}

          <ambientLight intensity={0.8} />

          <directionalLight
            position={[5, 9, 6]}
            intensity={2.5}
            castShadow
          />

          {/* -------------------------------- */}
          {/* FACTORY FLOOR */}
          {/* -------------------------------- */}

          <mesh position={[0, -0.1, 0]} receiveShadow>
            <boxGeometry args={[11, 0.2, 4]} />
            <meshStandardMaterial color="#203047" />
          </mesh>

          <gridHelper
            args={[16, 16, '#4d617d', '#29384f']}
            position={[0, 0.01, 0]}
          />

          {/* -------------------------------- */}
          {/* CONVEYOR BELT */}
          {/* -------------------------------- */}


          <group
            position={[-3.5, 0, 0]}
            onClick={(event) => {
              event.stopPropagation()
              setSelectedType(0)
            }}
          >
            {/* Active production stage */}
            <ActiveStageMarker active={activeStage === 1} />

            {/* Blender conveyor model */}
            <Suspense fallback={null}>
              <BlenderConveyor />
            </Suspense>

            {/* Live machine status indicator */}
            <mesh position={[0, 0.95, -0.51]}>
              <sphereGeometry args={[0.075, 16, 16]} />
              <meshStandardMaterial {...getMachineVisual(0)} />
            </mesh>

            {/* Animated package */}
            {activeStage === null && (
              <ConveyorPackage running={conveyorRunning} />
            )}
          </group>



            {/* -------------------------------- */}
            {/* SCANNER STATION - BLENDER MODEL */}
            {/* -------------------------------- */}

            <group
              position={[-1.2, 0, 0]}
              onClick={(event) => {
                event.stopPropagation()
                setSelectedType(1)
              }}
            >
              {/* Active production stage */}
              <ActiveStageMarker active={activeStage === 2} />

              {/* Blender scanner model */}
              <Suspense fallback={null}>
                <BlenderScanner />
              </Suspense>

              {/* Live machine status indicator */}
              <mesh position={[0, 1.67, 0.43]}>
                <sphereGeometry args={[0.075, 16, 16]} />

                <meshStandardMaterial
                  {...getMachineVisual(1)}
                />
              </mesh>

              {/* Animated scanner beam */}
              <ScannerBeam running={scannerRunning} />
            </group>



              {/* -------------------------------- */}
              {/* HYDRAULIC PRESS - BLENDER MODEL */}
              {/* -------------------------------- */}

              <group
                position={[1.2, 0, 0]}
                onClick={(event) => {
                  event.stopPropagation()
                  setSelectedType(2)
                }}
              >
                {/* Active production stage */}
                <ActiveStageMarker active={activeStage === 3} />

                {/* Blender hydraulic press */}
                <Suspense fallback={null}>
                  <BlenderPress />
                </Suspense>

                {/* Live machine status indicator */}
                <mesh position={[0, 1.82, 0.58]}>
                  <sphereGeometry args={[0.075, 16, 16]} />
                  <meshStandardMaterial
                    {...getMachineVisual(2)}
                  />
                </mesh>

                {/* Animated hydraulic piston and press plate */}
                <PressMechanism running={pressRunning} />
              </group>


          {/* -------------------------------- */}
          {/* ROBOT ARM */}
          {/* -------------------------------- */}

          <group
            position={[3.5, 0, 0]}
            onClick={(event) => {
              event.stopPropagation()
              setSelectedType(3)
            }}
          >
            {/* Active stage marker */}
            <ActiveStageMarker
              active={activeStage === 4}
            />

            {/* -------------------------------- */}
            {/* ROBOT PICKUP STATION */}
            {/* -------------------------------- */}

            {/* Support column */}
            <mesh position={[0, 0.195, 1.05]} castShadow>
              <boxGeometry args={[0.22, 0.39, 0.22]} />
              <meshStandardMaterial
                color="#64748b"
                metalness={0.6}
                roughness={0.4}
              />
            </mesh>

            {/* Pickup platform */}
            <mesh
              position={[0, 0.42, 1.05]}
              castShadow
              receiveShadow
            >
              <boxGeometry args={[0.8, 0.12, 0.75]} />
              <meshStandardMaterial
                color="#334155"
                metalness={0.5}
                roughness={0.45}
              />
            </mesh>

            {/* Platform marking */}
            <mesh position={[0, 0.485, 1.05]}>
              <boxGeometry args={[0.5, 0.008, 0.48]} />
              <meshStandardMaterial
                color="#f59e0b"
                metalness={0.25}
                roughness={0.6}
              />
            </mesh>


            {/* Animated robot */}
            <RobotArmMechanism
              running={robotRunning}
              pickPlaceActive={activeStage === 4}
              pickPlaceTimeRef={pickPlaceTimeRef}
              gripperRef={gripperRef}
              visual={getMachineVisual(3)}
            />
          </group>


          {/* -------------------------------- */}
          {/* ROBOT OUTPUT STATION */}
          {/* -------------------------------- */}

          <group position={[4.7, 0, 1.05]}>
            {/* Support column */}
            <mesh position={[0, 0.195, 0]} castShadow>
              <boxGeometry args={[0.22, 0.39, 0.22]} />
              <meshStandardMaterial
                color="#64748b"
                metalness={0.6}
                roughness={0.4}
              />
            </mesh>

            {/* Output platform */}
            <mesh position={[0, 0.42, 0]} castShadow receiveShadow>
              <boxGeometry args={[0.8, 0.12, 0.75]} />
              <meshStandardMaterial
                color="#334155"
                metalness={0.5}
                roughness={0.45}
              />
            </mesh>

            {/* Green marking */}
            <mesh position={[0, 0.485, 0]}>
              <boxGeometry args={[0.5, 0.008, 0.48]} />
              <meshStandardMaterial
                color="#22c55e"
                metalness={0.25}
                roughness={0.6}
              />
            </mesh>
          </group>

          {/* -------------------------------- */}
          {/* PRODUCTION WORKPIECE */}
          {/* -------------------------------- */}

          {showProductionWorkpiece && (
            <ProductionWorkpiece
              key={simulationId ?? 'production'}
              activeStage={productionCompleted ? 5 : (activeStage ?? 0)}
              pickPlaceTimeRef={pickPlaceTimeRef}
              gripperRef={gripperRef}
            />
          )}

          {/* -------------------------------- */}
          {/* CAMERA CONTROLS */}
          {/* -------------------------------- */}

          <OrbitControls
            makeDefault
            target={[0, 0.8, 0]}
            minDistance={5}
            maxDistance={22}
            maxPolarAngle={Math.PI / 2.05}
          />
        </Canvas>
      </div>

      {/* -------------------------------- */}
      {/* SELECTED MACHINE INFORMATION */}
      {/* -------------------------------- */}

      {selectedType !== null && (
        <div
          style={{
            marginTop: '16px',
            padding: '18px',
            borderRadius: '10px',
            background: '#17263b',
            border: '1px solid #334155',
            color: '#f1f5f9',
          }}
        >
          <div
            style={{
              display: 'flex',
              justifyContent: 'space-between',
              alignItems: 'center',
              gap: '12px',
            }}
          >
            <h3 style={{ marginTop: 0 }}>
              {selectedMachine?.name ??
                machineNames[selectedType]}
            </h3>

            <button
              type="button"
              onClick={() => setSelectedType(null)}
              style={{
                padding: '6px 10px',
                borderRadius: '6px',
                border: '1px solid #475569',
                background: '#1e293b',
                color: '#f1f5f9',
                cursor: 'pointer',
              }}
            >
              Close
            </button>
          </div>

          {selectedMachine ? (
            <>
              <p>
                <strong>Machine Type:</strong>{' '}
                {machineNames[selectedMachine.type] ??
                  'Unknown'}
              </p>

              <p>
                <strong>Status:</strong>{' '}
                <span
                  style={{
                    color:
                      selectedMachine.status === 2
                        ? '#4ade80'
                        : selectedMachine.status === 4
                          ? '#f87171'
                          : '#e2e8f0',
                    fontWeight: 700,
                  }}
                >
                  {machineStatuses[selectedMachine.status] ??
                    'UNKNOWN'}
                </span>
              </p>

              <small
                style={{
                  color: '#94a3b8',
                }}
              >
                Machine ID: {selectedMachine.id}
              </small>
            </>
          ) : (
            <p>Waiting for machine data...</p>
          )}
        </div>
      )}
    </section>
  )
}
