namespace CDL.AirBrakeSimulator
{
    /// <summary>
    /// Vehicle ignition switch positions.
    /// </summary>
    public enum IgnitionState
    {
        OFF,
        ACC_ON,
        RUNNING
    }

    /// <summary>
    /// Dual push-pull control valves on standard Class A tractor-trailers.
    /// </summary>
    public enum ValveType
    {
        TractorYellow,  // Yellow Diamond: Parking Brake / Tractor Protection
        TrailerRed      // Red Octagon: Trailer Air Supply
    }

    /// <summary>
    /// Standard DMV / FMCSA Class A In-Cab Air Brake Inspection Steps (LABL sequence).
    /// </summary>
    public enum DMVStep
    {
        EnginePrep,             // Step 1: Charge system until Governor Cut-Out (120-140 PSI)
        AppliedLeakageTest,     // Step 2 (L): Engine OFF, Key ON, Push valves in, hold pedal 60s (loss <= 4 PSI)
        LowAirAlarmTest,        // Step 3 (A): Fan pedal until warning buzzer/light activate (>= 60 PSI)
        EmergencyPopOutTest,    // Step 4 (B): Fan pedal until both valves pop out automatically (20-45 PSI)
        CompressorBuildUpTest,  // Step 5 (L - Build-Up): Start engine, measure 85 to 100 PSI build-up (<= 45 sec)
        TestCompleted,          // Full inspection successfully passed
        TestFailed              // Candidate committed a critical safety violation or exceeded tolerance
    }

    /// <summary>
    /// Vehicle class configuration for allowable leakage tolerance.
    /// </summary>
    public enum VehicleCombinationType
    {
        ClassACombination, // Maximum allowable loss: 4 PSI in 1 minute
        SingleVehicle      // Maximum allowable loss: 3 PSI in 1 minute
    }

    /// <summary>
    /// Simulator operating modes: Guided Training Mode vs Official DMV Exam Mode.
    /// </summary>
    public enum GameMode
    {
        Training = 0, // Interactive guidance, button highlights, explanations, soft errors
        Exam = 1      // Strict DMV testing, no visual aids, instant failure on violation
    }

    /// <summary>
    /// Interactive targets that can be dynamically highlighted during Guided Training.
    /// </summary>
    public enum HighlightTarget
    {
        None = 0,
        IgnitionOff,
        IgnitionAcc,
        IgnitionRun,
        RevEngine,
        YellowValve,
        RedValve,
        BothValves,
        HoldPedal,
        FanPedal,
        FastForward
    }
}
