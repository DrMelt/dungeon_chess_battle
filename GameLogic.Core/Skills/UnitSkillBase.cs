using Godot;
using Godot.Collections;
using System;
using System.Collections.Generic;

#nullable disable

namespace GameLogic {
    [GlobalClass]
    public partial class UnitSkillBase : Resource {
        [Export]
        string skillName = "UnitSkillBaseName";
        public string SkillName => skillName;

        [Export(PropertyHint.MultilineText)]
        string skillDescription = "UnitSkillBaseDescription";
        public string SkillDescription => skillDescription;

        [Export]
        float skillNeedSpellTime = 2.0f;
        public float SkillSpellTime => skillNeedSpellTime;

        [Export]
        float skillCooldownTime = 3.0f;

        [Export]
        float gcdTime = 3.0f;
        public float GCDTime => gcdTime;

#pragma warning disable CS0649
        [Export]
        Texture2D icon;
#pragma warning restore CS0649
        public Texture2D Icon => icon;

        [Export]
        EnumSkillCanAdd skillCanAdd = EnumSkillCanAdd.None;

        [Export]
        bool needUnitTarget = false;
        public bool NeedUnitTarget => needUnitTarget;

        [Export]
        bool needPosTarget = false;
        public bool NeedPosTarget => needPosTarget;

        [ExportGroup("Runtime Parameters")]
        [Export]
        float skillSpelledTime = 0;
        public float SkillSpelledTime => skillSpelledTime;

        public float SkillSpellProgress => skillSpelledTime / skillNeedSpellTime;

        [Export]
        float skillCoolingTime = 0;
        public float SkillCoolingTime => skillCoolingTime;

        protected IUnitState callSkillObject;
        protected IUnitState targetObject;
        public IUnitState CallSkillObject => callSkillObject;

        [Export]
        protected Vector3 targetPos;
        public Vector3 TargetPos => targetPos;

        protected Array<IUnitState> testObjects;

        protected void Reset_SpelledSkill() {
            skillCoolingTime = skillCooldownTime;
            skillSpelledTime = 0;
        }

        public void UpdateSkill(double delta) {
            skillCoolingTime -= (float)delta;
            skillSpelledTime += (float)delta;
        }

        public bool IsCoolingdown() {
            return skillCoolingTime > 0;
        }

        EnumSkillCanAdd SkillAddEnum(IUnitState callSkillObject, IUnitState testObject) {
            EnumSkillCanAdd addEnum = EnumSkillCanAdd.None;
            if (callSkillObject.Camp == testObject.Camp) {
                addEnum |= EnumSkillCanAdd.Same;
            }
            if (callSkillObject.Camp != testObject.Camp) {
                addEnum |= EnumSkillCanAdd.Different;
            }

            return addEnum;
        }

        public void SetSkill(IUnitState callSkillObject, IUnitState targetObject, Vector3? targetPos, System.Collections.Generic.IEnumerable<IUnitState> testObjects) {
            if (needUnitTarget) {
                if (targetObject == null) {
                    GD.Print($"{skillName} need a target");
                    return;
                }
                if (!SkillAddEnum(callSkillObject, targetObject).HasFlag(skillCanAdd)) {
                    GD.Print($"{skillName} can't add to {targetObject.UnitStateName}");
                    return;
                }
            }

            skillSpelledTime = 0;

            this.callSkillObject = callSkillObject;
            this.targetObject = targetObject;
            if (targetPos.HasValue) {
                this.targetPos = targetPos.Value;
            }
            this.testObjects = new Array<IUnitState>();
            foreach (var obj in testObjects) {
                this.testObjects.Add(obj);
            }

            callSkillObject.SpellNewSkill(this);
        }

        public void SpellBroked() {
            skillSpelledTime = 0;
        }

        public bool CallSkillSpelling() {
            if (!IsCoolingdown() &&
                skillSpelledTime >= skillNeedSpellTime) {
                CallSpelledSkill();
                Reset_SpelledSkill();
                return true;
            }
            return false;
        }

        protected virtual void CallSpelledSkill() {
            GD.Print($"{skillName} is called to {targetObject}");
            Reset_SpelledSkill();
        }
    }
}
