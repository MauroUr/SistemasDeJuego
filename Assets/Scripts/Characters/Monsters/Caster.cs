using System.Collections;
using UnityEngine;

public class Caster : Enemy
{
    protected float _casting = 0;
    [SerializeField] protected Spells spell;
    [SerializeField] protected int spellLevel;
    [SerializeField] protected int casterInt = 1;
    [SerializeField] protected float castingSpeed;
    protected ISpells _spellInstance;

    private Character _currentEnemy;
    
    protected override void Start()
    {
        base.Start();
        _spellInstance = new SpellInstanceWrapper(spell, spellLevel);
    }
    public override IEnumerator CastSpell(GameObject enemy)
    {
        agent.ResetPath();
        animator.SetBool(animations[4], true);
        float startingLife = life;

        _currentEnemy = enemy.GetComponentInParent<Character>();

        while (_casting < 100 && startingLife <= life)
        {
            this.ShowCastingCircle();
            if (_currentEnemy != null)
                _currentEnemy.BeingTargeted(true);
            _casting += 20 / (_spellInstance.Level * _spellInstance.CastDelayPerLevel) * Time.deltaTime * castingSpeed;

            Quaternion nextRotation = Quaternion.LookRotation(_currentEnemy.transform.position - transform.position);
            nextRotation.x = transform.rotation.x;
            if (nextRotation != Quaternion.identity)
                this.transform.localRotation = nextRotation;
            yield return null;
        }

        animator.SetBool(animations[4], false);
        animator.SetTrigger(animations[5]);
        _casting = 0;
        
        if (startingLife > life || enemy == null)
        {
            this.ChangeState(new Idle(this));
            yield break;
        }

        animator.ResetTrigger(animations[2]);

    }
    protected void ThrowSpell()
    {
        SpellController.Cast(_spellInstance, transform, _currentEnemy.transform, casterInt);
        this.ChangeState(new Idle(this));

        this.HideCastingCircle();
        if (_currentEnemy != null)
            _currentEnemy.BeingTargeted(false);

    }
}