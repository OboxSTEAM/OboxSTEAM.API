using Microsoft.Extensions.Logging;
using OboxSteam.Application.Commons;
using OboxSteam.Domain.Entities;

namespace OboxSteam.Application.Services;

public partial class SeedService
{
    // Difficulty tiers used by the runtime draw helper: 1-2 = easy, 3 = medium, 4-5 = hard.
    // Each bank below holds 20 single-choice questions (8 easy, 8 medium, 4 hard) so both the
    // 5-question and 15-question Module 1 quizzes can draw with a 40/40/20 difficulty split.
    // Option index 0 is always the correct answer (quizzes shuffle options at attempt time).

    private static readonly (string Text, int Difficulty, string[] Options)[] RoboticsCourse1BankQuestions =
    [
        ("What is a robot?", 2, ["A machine that can sense, think, and act", "A type of computer virus", "A remote-control toy only", "A video-game character"]),
        ("Which component lets a robot detect its surroundings?", 1, ["Sensor", "Chassis", "Battery", "Sticker"]),
        ("What does an actuator do?", 2, ["Produces movement", "Stores data", "Displays images", "Cools the processor"]),
        ("Which of these is an input device for a robot?", 1, ["Camera", "Motor", "Wheel", "Gear"]),
        ("The 'brain' of a small robot is usually the", 2, ["Microcontroller", "Wheel", "Frame", "Cable"]),
        ("What powers most small hobby robots?", 1, ["Battery", "Sunlight only", "Wind", "Sound"]),
        ("A set of instructions a robot follows is called a", 2, ["Program", "Payload", "Torque", "Bearing"]),
        ("An 'autonomous' robot is one that", 2, ["Operates without human control", "Needs constant remote control", "Cannot move", "Has no sensors"]),
        ("Feedback control uses sensor data to", 3, ["Adjust actions in real time", "Ignore the environment", "Save battery only", "Print logs"]),
        ("'Degrees of freedom' refers to", 3, ["Independent ways a robot can move", "Number of batteries", "Screen size", "Wi-Fi channels"]),
        ("Which loop repeatedly reads sensors and updates actuators?", 3, ["Control loop", "Boot loop", "Idle loop", "Render loop"]),
        ("A closed-loop system differs from open-loop because it", 3, ["Uses feedback", "Has no power", "Uses more wheels", "Is always wireless"]),
        ("The measure of rotational force is", 3, ["Torque", "Volt", "Lumen", "Hertz"]),
        ("An encoder on a motor measures", 3, ["Position or rotation", "Temperature", "Color", "Sound"]),
        ("Which best describes an 'end effector'?", 3, ["The tool at the end of a robot arm", "The robot's battery", "The main processor", "A floor sensor"]),
        ("PWM (pulse-width modulation) is commonly used to", 3, ["Control motor speed", "Store files", "Encrypt data", "Cool motors"]),
        ("In a PID controller, the integral (I) term addresses", 4, ["Accumulated past error", "Instant error only", "Future prediction only", "Random noise"]),
        ("Kinematics in robotics studies", 4, ["Motion without regard to forces", "Only electrical current", "Battery chemistry", "Network latency"]),
        ("A key benefit of sensor fusion is that it", 4, ["Combines sensors to improve accuracy", "Disables sensors", "Removes the processor", "Stops all movement"]),
        ("Inverse kinematics computes", 5, ["Joint angles for a desired position", "Battery life", "Screen resolution", "Wi-Fi range"]),
    ];

    private static readonly (string Text, int Difficulty, string[] Options)[] RoboticsCourse2BankQuestions =
    [
        ("A DC motor converts", 1, ["Electrical energy into motion", "Motion into light", "Sound into heat", "Data into sound"]),
        ("A gear is mainly used to", 2, ["Change speed or torque", "Store data", "Emit light", "Filter air"]),
        ("A servo motor is known for", 1, ["Precise angular positioning", "Unlimited speed only", "No control at all", "Cooling the circuit"]),
        ("Which arrangement reduces speed but increases torque?", 2, ["Gear reduction", "Raising voltage only", "Using a longer wire", "Adding an LED"]),
        ("The structural body that holds robot parts is the", 1, ["Chassis", "Sensor", "Firmware", "Protocol"]),
        ("A wheel converts motor rotation into", 2, ["Linear motion", "Heat only", "Light", "Sound"]),
        ("Which of these is a linear actuator?", 2, ["Lead-screw actuator", "Spur gear", "Resistor", "LED"]),
        ("Bearings are used to", 1, ["Reduce friction in rotating parts", "Increase weight", "Store power", "Emit sound"]),
        ("A gear ratio of 3:1 means the output turns", 3, ["At one-third of the input speed", "Three times faster with equal torque", "At the same speed", "Only backwards"]),
        ("A stepper motor moves in", 3, ["Discrete steps", "Only continuous spin", "Random jumps", "No movement"]),
        ("Motor torque is best delivered to a load when the motor is", 3, ["Appropriately geared for the load", "Run at zero voltage", "Unloaded at max speed", "Wired with the longest cable"]),
        ("'Backlash' in a gear train refers to", 3, ["Slack between meshing teeth", "Motor overheating", "Battery drain", "Sensor noise"]),
        ("An H-bridge circuit allows a motor to", 3, ["Reverse its direction", "Only stop", "Only speed up", "Charge the battery"]),
        ("Which mechanism increases mechanical advantage?", 3, ["Levers and gears", "Removing all gears", "Higher screen brightness", "Longer boot time"]),
        ("A worm gear is notable for", 3, ["High reduction and self-locking", "Providing no reduction", "Emitting light", "Cooling the motor"]),
        ("Belt drives transmit power using", 3, ["Friction and tension", "Magnetism only", "Light", "Sound"]),
        ("The 'holding torque' of a stepper motor is", 4, ["Torque that holds position while energized", "Torque only when powered off", "Its battery capacity", "Its maximum RPM"]),
        ("Efficiency loss in gear trains mainly comes from", 4, ["Friction and backlash", "Adding more sensors", "Software bugs", "Wi-Fi interference"]),
        ("For high-precision positioning you would choose a", 4, ["Servo or stepper with an encoder", "Plain DC motor with no feedback", "Buzzer", "Resistor"]),
        ("Compliant mechanisms achieve motion through", 5, ["Elastic deformation of flexible parts", "Rigid gears only", "Battery swelling", "Network packets"]),
    ];

    private static readonly (string Text, int Difficulty, string[] Options)[] RoboticsCourse3BankQuestions =
    [
        ("Before working in the lab you should first", 1, ["Read the safety guidelines", "Ignore the rules", "Run around the room", "Eat at the workbench"]),
        ("Proper eye protection in the lab means wearing", 1, ["Safety goggles", "Sunglasses", "Nothing", "A cap"]),
        ("A fire extinguisher is used to", 2, ["Put out small fires", "Cool drinks", "Charge robots", "Clean floors"]),
        ("Long hair near rotating machinery should be", 1, ["Tied back", "Left loose", "Sprayed", "Ignored"]),
        ("Before touching sensitive electronics you should", 2, ["Discharge static electricity (ESD)", "Rub a balloon on them", "Wet your hands", "Do nothing"]),
        ("If you smell burning from a robot, you should", 1, ["Power it off and report it", "Keep using it", "Add more voltage", "Cover it with cloth"]),
        ("Sharp tools should be", 2, ["Stored safely after use", "Left on the floor", "Thrown to a teammate", "Kept in a pocket"]),
        ("An emergency-stop button is used to", 1, ["Immediately halt a machine", "Restart the Wi-Fi", "Save your files", "Dim the lights"]),
        ("A risk assessment is performed to", 3, ["Identify and reduce hazards", "Increase danger", "Only speed up work", "Skip safety steps"]),
        ("Lockout/tagout procedures prevent", 3, ["Accidental energizing of a machine", "Data loss", "Slow booting", "Low battery"]),
        ("The first step of any incident response is to", 3, ["Ensure the safety of people", "Finish the task", "Hide the issue", "Blame someone"]),
        ("Soldering should be carried out", 3, ["In a well-ventilated area", "Inside a sealed box", "Right next to water", "While walking around"]),
        ("LiPo batteries require care because they can", 3, ["Catch fire if damaged", "Never fail", "Cool the room", "Boost Wi-Fi"]),
        ("PPE stands for", 3, ["Personal Protective Equipment", "Public Power Exchange", "Program Path Editor", "Partial Pressure Estimate"]),
        ("A cluttered workspace mainly increases", 3, ["Accident risk", "Motor torque", "Battery life", "Signal strength"]),
        ("A Safety Data Sheet (SDS) describes", 3, ["Hazards and handling of a substance", "Robot source code", "Wi-Fi setup", "Gear ratios"]),
        ("A capacitor that may still hold charge after power-off should be", 4, ["Discharged safely before handling", "Touched immediately", "Ignored", "Given more voltage"]),
        ("The hierarchy of hazard control prioritizes", 4, ["Elimination over PPE", "PPE over everything else", "Ignoring hazards", "Random choice"]),
        ("Grounding equipment primarily protects against", 4, ["Electric shock", "Slow software", "Low light", "Poor Wi-Fi"]),
        ("Arc-flash risk is best mitigated by", 5, ["Proper insulation and safe procedures", "Removing all sensors", "Using more glue", "Faster processors"]),
    ];

    private async Task SeedRoboticsQuestionBanksAsync()
    {
        _loggerService.LogInformation("Starting seed robotics question banks");
        var seedTime = DateTime.UtcNow;

        await SeedQuestionBankForCourseAsync(
            "CRS-ROBOTICS-01",
            "Robot Fundamentals Question Bank",
            "Question bank for the Module 1 Robot Fundamentals quiz.",
            RoboticsCourse1BankQuestions,
            seedTime);

        await SeedQuestionBankForCourseAsync(
            "CRS-ROBOTICS-02",
            "Mechanics & Actuators Question Bank",
            "Question bank for the Module 1 Mechanics & Actuators quiz.",
            RoboticsCourse2BankQuestions,
            seedTime);

        await SeedQuestionBankForCourseAsync(
            "CRS-ROBOTICS-03",
            "Safety & Lab Practice Question Bank",
            "Question bank for the Module 1 Safety & Lab Practice final quiz.",
            RoboticsCourse3BankQuestions,
            seedTime);

        _loggerService.LogInformation("Finished seed robotics question banks");
    }

    private async Task SeedQuestionBankForCourseAsync(
        string courseCode,
        string name,
        string description,
        (string Text, int Difficulty, string[] Options)[] questions,
        DateTime seedTime)
    {
        var course = await _unitOfWork.Courses.FirstOrDefaultAsync(c => c.Code == courseCode && !c.IsDeleted);
        if (course == null)
        {
            _loggerService.LogWarning("Course {CourseCode} not found; skipping question bank '{Name}'.", courseCode, name);
            return;
        }

        var existingBank = await _unitOfWork.QuestionBanks.FirstOrDefaultAsync(
            qb => qb.CourseId == course.Id && qb.Name == name && !qb.IsDeleted);
        if (existingBank != null)
        {
            _loggerService.LogInformation("Question bank '{Name}' already exists; skipping.", name);
            return;
        }

        var bank = new QuestionBank
        {
            Id = Guid.NewGuid(),
            CourseId = course.Id,
            Name = name,
            Description = description,
            CreatedAt = seedTime,
            CreatedBy = Guid.Empty,
            IsDeleted = false,
        };

        await _unitOfWork.QuestionBanks.AddAsync(bank);
        await _unitOfWork.SaveChangesAsync();

        var bankQuestions = new List<BankQuestion>();
        var orderIndex = 1;
        foreach (var question in questions)
        {
            bankQuestions.Add(new BankQuestion
            {
                Id = Guid.NewGuid(),
                QuestionBankId = bank.Id,
                QuestionText = question.Text,
                QuestionType = QuestionTypeConstants.SingleChoice,
                Points = 1,
                DifficultyLevel = question.Difficulty,
                OrderIndex = orderIndex++,
                CreatedAt = seedTime,
                CreatedBy = Guid.Empty,
                IsDeleted = false,
            });
        }

        await _unitOfWork.BankQuestions.AddRangeAsync(bankQuestions);
        await _unitOfWork.SaveChangesAsync();

        var options = new List<BankQuestionOption>();
        for (var i = 0; i < questions.Length; i++)
        {
            var question = questions[i];
            var bankQuestion = bankQuestions[i];

            for (var j = 0; j < question.Options.Length; j++)
            {
                options.Add(new BankQuestionOption
                {
                    Id = Guid.NewGuid(),
                    BankQuestionId = bankQuestion.Id,
                    OptionText = question.Options[j],
                    IsCorrect = j == 0,
                    CreatedAt = seedTime,
                    CreatedBy = Guid.Empty,
                    IsDeleted = false,
                });
            }
        }

        await _unitOfWork.BankQuestionOptions.AddRangeAsync(options);
        await _unitOfWork.SaveChangesAsync();

        _loggerService.LogInformation(
            "Seeded question bank '{Name}' with {QuestionCount} questions for course {CourseCode}.",
            name,
            questions.Length,
            courseCode);
    }
}
