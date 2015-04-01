namespace InfernalRobotics.Control
{
    public interface IMechanism
    {
        float Position { get; }
        float MinPosition { get; }
        float MinPositionLimit { get; set; }

        float MaxPosition { get; }
        float MaxPositionLimit { get; set; }

        bool IsMoving { get; }
        bool IsFreeMoving { get; }
        bool IsLocked { get; set; }
        float CurrentSpeed { get; }
        float MaxSpeed { get; }
        float SpeedLimit { get; set; }
        float AccelerationLimit { get; set; }
        bool IsAxisInverted { get; set; }

        /// <summary>
        /// the speed from part.cfg is used as the default unit of speed
        /// </summary>
        float DefaultSpeed { get; }

        void MoveLeft();
        void MoveCenter();
        void MoveRight();
        void Stop();
        void MoveTo(float position);
        void MoveTo(float position, float speed);
    }
}