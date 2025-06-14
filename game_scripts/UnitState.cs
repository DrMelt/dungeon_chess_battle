using Godot;
using System;
using System.Collections.Generic;
using System.Data;

namespace GameLogic
{
    public enum EmunCamp
    {
        None = 0,
        Camp_A,
        Camp_B,
        Camp_C,
        Camp_BOSS,
        EnumCampLength
    }


    [GlobalClass]
    public partial class UnitState : Resource
    {
        [Export]
        string _UnitStateName = "UnitStateName";
        public string UnitStateName => _UnitStateName;

        [Export]
        Vector3 _position = Vector3.Zero;
        public Vector3 Position => _position;
        public void SetGlobalPosition(Vector3 position)
        {
            _position = position;
        }

        [Export]
        Vector3 _lookAt_Dir = Vector3.Forward;
        public Vector3 LookAt_Dir => _lookAt_Dir;

        [Export]
        float radius = 0.0f;

        [Export]
        EmunCamp _camp = EmunCamp.None;
        public EmunCamp Camp => _camp;


        [Export]
        float _maxHealth = 1000;
        public float MaxHealth
        {
            get => _maxHealth; private set
            {
                if (value != _maxHealth)
                {
                    _maxHealth = value;
                    OnMaxHealthChangedEnvent?.Invoke(_maxHealth);
                }
            }
        }
        public Action<float> OnMaxHealthChangedEnvent;

        [Export]
        float _health = 1000;
        public float Health
        {
            get => _health;
            private set
            {
                if (value != _health)
                {
                    _health = value;
                    OnHealthChangedEnvent?.Invoke(_health);
                }
            }
        }
        public Action<float> OnHealthChangedEnvent;

        float _shield = 0.0f;
        public float Shield
        {
            get => _shield;
            private set
            {
                if (value != _shield)
                {
                    _shield = value;
                    OnShieldChangedEnvent?.Invoke(_shield);
                }
            }
        }
        public Action<float> OnShieldChangedEnvent;

        public float Health_Shield => _health + _shield;
        public float Health_Percent => _health / Mathf.Max(MaxHealth, Health_Shield);
        public float Health_Shield_Percent => Health_Shield / MaxHealth;

        [Export]
        float _cureIntensity = 1.0f;
        public float CureIntensity => _cureIntensity;

        [Export]
        float _physicalAttackBase = 1.0f;
        public float PhysicalAttackBase { get => _physicalAttackBase; }

        [Export]
        float _physicalDefencePercent = 0.0f;
        public float PhysicalDefencePercent { get => _physicalDefencePercent; }

        [Export]
        float _magicAttackBase = 1.0f;
        public float MagicAttackBase { get => _magicAttackBase; }

        [Export]
        float _magicDefencePercent = 0.0f;
        public float MagicDefencePercent { get => _magicDefencePercent; }

        [Export]
        float _baseSpeed = 1.0f;
        public float BaseSpeed { get => _baseSpeed; }
        public float MoveSpeed => BaseSpeed;


        [ExportGroup("Runtime Parameters")]
        [Export]
        MotionTimeTable motionTimeTable = new MotionTimeTable();


        [Export]
        UnitSkillBase spellingSkill = null;
        [Export]
        float spellTime = 0.0f;
        


        [Export]
        Godot.Collections.Dictionary<string, float> _hates = new Godot.Collections.Dictionary<string, float>();

        #region Hate
        public string GetMaxHateUnitName()
        {
            float maxHate = 0;
            string maxHateUnitName = null;
            foreach (KeyValuePair<string, float> item in _hates)
            {
                if (item.Value > maxHate)
                {
                    maxHate = item.Value;
                    maxHateUnitName = item.Key;
                }
            }
            return maxHateUnitName;
        }
        #endregion



        public void InvokeEnvents()
        {
            OnHealthChangedEnvent?.Invoke(Health);
            OnMaxHealthChangedEnvent?.Invoke(MaxHealth);
            OnShieldChangedEnvent?.Invoke(Shield);
            // TODO: invoke other events
        }

        #region BUFF

        List<BuffBase> buffList = new List<BuffBase>();
        public List<BuffBase> BuffList { get => buffList; }


        public void AddBuff(BuffBase buff)
        {
            BuffBase find = buffList.Find((BuffBase b) => b.buffName == buff.buffName);
            if (find != null)
            {
                find.AddSuperpositions(buff);
            }
            else
            {
                buffList.Add(buff);
            }
        }

        public void RemoveBuff(BuffBase buff)
        {
            buffList.RemoveAll((BuffBase b) => b.buffName == buff.buffName);
        }

        public void UpdateBuffList(double deltaTime)
        {
            List<BuffBase> tempList = new List<BuffBase>();
            foreach (BuffBase buffBase in buffList)
            {
                buffBase.Update(deltaTime, this);
                if (buffBase.isActive)
                {
                    tempList.Add(buffBase);
                }
            }

            buffList = tempList;
        }
        #endregion


        #region DAMAGE
        public float TakeDamage(float physicalDamage, float magicDamage)
        {
            float physicalDamageFixed = physicalDamage * (1 - PhysicalDefencePercent);
            float magicDamageFixed = magicDamage * (1 - MagicDefencePercent);

            // TODO: take into account buffs

            float damage = physicalDamageFixed + magicDamageFixed;

            if (_shield > damage)
            {
                _shield -= damage;
            }
            else
            {
                _shield = 0;
                _health -= damage - _shield;
                _health = Mathf.Clamp(_health, 0, MaxHealth);
            }

            return damage;
        }

        public float PhysicalDamageAmount(float physicalDamage)
        {
            return physicalDamage * PhysicalAttackBase;
        }

        public float MagicDamageAmount(float magicDamage)
        {
            return magicDamage * MagicAttackBase;
        }

        #endregion

        public float CureAmount(float curePotency)
        {
            return CureIntensity * curePotency;
        }
        public float RestoreHealth(float health)
        {
            float healthFixed = Mathf.Clamp(_health + health, 0, MaxHealth) - _health;
            _health += healthFixed;

            return healthFixed;
        }



        public float TakeShield(float shield)
        {
            float shieldFixed = shield;


            _shield += shieldFixed;
            return shieldFixed;
        }



    }
}

