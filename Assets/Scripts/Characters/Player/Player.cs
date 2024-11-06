using System;
using System.Collections;
using System.Collections.Generic;
using System.Runtime.Serialization;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.UI;

public class Player : Character
{
    private PlayerInput _inputs;
    private InputAction _rightClickAction;
    private InputAction _spellsAction;
    private InputAction _spellsUIAction;
    private InputAction _statsPanelAction;
    private Animator _animator;

    [SerializeField] private List<string> animations = new();
    private Vector3 _nextPosition;

    [SerializeField] private float movSpeed;
    [SerializeField] private Slider manaBar;
    [SerializeField] private GameObject castBar;
    private Slider _castSlider;

    private List<SpellInstanceWrapper> _selectedSpells;
    public Stats stats;

    public event Action OnStatsPressed;
    public event Action OnSpellUIPressed;

    #region Setup
    private void Awake()
    {
        _inputs = GetComponent<PlayerInput>();
        _animator = GetComponent<Animator>();

        _rightClickAction = _inputs.actions["RightClick"];
        _spellsAction = _inputs.actions["Spells"];
        _spellsUIAction = _inputs.actions["SpellsUI"];
        _statsPanelAction = _inputs.actions["Stats"];

        Cursor.visible = true;
    }

    private new void Start()
    {
        base.Start();
        _castSlider = castBar.GetComponent<Slider>();
        stats = Stats.NewStats();

        StartCoroutine(GetUnlockedSpells());
    }

    public void OnNewSpellEquipped()
    {
        StartCoroutine(GetUnlockedSpells());
    }

    private IEnumerator GetUnlockedSpells()
    {
        var spellService = ServiceLocator.instance.GetService<SpellService>(typeof(SpellService));
        while (spellService == null)
        {
            yield return null;
            spellService = ServiceLocator.instance.GetService<SpellService>(typeof(SpellService));
        }

        _selectedSpells = spellService.GetEquippedSpells();
    }

    private void OnEnable()
    {
        _inputs.ActivateInput();
        _rightClickAction.performed += GoToPosition;
        _spellsAction.performed += HandleSpellCasting;
        _statsPanelAction.performed += _ => OnStatsPressed?.Invoke();
        _spellsUIAction.performed += _ => OnSpellUIPressed?.Invoke();
    }

    private void OnDisable()
    {
        _inputs.DeactivateInput();
        _rightClickAction.performed -= GoToPosition;
        _spellsAction.performed -= HandleSpellCasting;
        _statsPanelAction.performed -= _ => OnStatsPressed?.Invoke();
        _spellsUIAction.performed -= _ => OnSpellUIPressed?.Invoke();
    }
    #endregion

    #region Movement
    private void GoToPosition(InputAction.CallbackContext context)
    {
        Ray ray = Camera.main.ScreenPointToRay(Mouse.current.position.ReadValue());
        if (Physics.Raycast(ray, out RaycastHit hit))
            _nextPosition = hit.point;
    }

    private void FixedUpdate()
    {
        MovePlayer();
    }

    private void MovePlayer()
    {
        const float positionThreshold = 0.1f;

        if (Vector3.Distance(transform.position, _nextPosition) > positionThreshold)
        {
            _animator.SetBool(animations[2], true);
            _nextPosition.y = transform.position.y;
            transform.position = Vector3.MoveTowards(transform.position, _nextPosition, Time.deltaTime * this.movSpeed);
            transform.rotation = Quaternion.LookRotation(_nextPosition - transform.position);
        }
        else
            _animator.SetBool(animations[2], false);
    }
    #endregion

    #region SpellCasting
    private void HandleSpellCasting(InputAction.CallbackContext ctx)
    {
        string bindingPath = ctx.control.path;
        for (int i = 0; i < 12; i++)
        {
            if (bindingPath == "/Keyboard/f" + (i + 1))
            {
                TryCasting(i);
                break;
            }
        }
    }

    private void TryCasting(int spellNumber)
    {
        if (!HasCastingRequirements(spellNumber))
            return;

        CursorManager.instance.ChangeCursor(CursorManager.CursorTypes.SpellSelect);
        StartCoroutine(SpellSelection(spellNumber));
    }

    private bool HasCastingRequirements(int spellNumber)
    {
        return (_selectedSpells[spellNumber] != null
            && _selectedSpells[spellNumber].spell.prefab != null
            && manaBar.value > _selectedSpells[spellNumber].spell.manaPerLevel * _selectedSpells[spellNumber].instanceLevel
            && !castBar.activeSelf);
    }

    private IEnumerator SpellSelection(int spellNumber)
    {
        Ray ray = Camera.main.ScreenPointToRay(Mouse.current.position.ReadValue());
        CursorManager.instance.ChangeCursor(CursorManager.CursorTypes.SpellSelect);

        while (CursorManager.instance.GetCurrentCursor() == CursorManager.CursorTypes.SpellSelect)
        {
            if (Input.GetMouseButtonDown(0))
            {
                ray = Camera.main.ScreenPointToRay(Mouse.current.position.ReadValue());
                CursorManager.instance.ChangeCursor(CursorManager.CursorTypes.Basic);
                break;
            }
            yield return null;
        }

        if (Physics.Raycast(ray, out RaycastHit hit))
        {
            const float clickRadius = 2f;
            Collider[] hitColliders = Physics.OverlapSphere(hit.point, clickRadius, LayerMask.GetMask("Enemy"));
            Collider closestEnemy = GetClosestEnemy(hit.point, hitColliders);

            if (closestEnemy != null)
            {
                _nextPosition = this.transform.position;
                yield return StartCoroutine(CastSpell(closestEnemy.gameObject, spellNumber));
            }
        }
    }

    private Collider GetClosestEnemy(Vector3 hitPoint, Collider[] hitColliders)
    {
        Collider closestEnemy = null;
        float closestDistance = float.MaxValue;

        foreach (Collider hitCollider in hitColliders)
        {
            float distance = Vector3.Distance(hitPoint, hitCollider.transform.position);
            if (distance < closestDistance)
            {
                closestEnemy = hitCollider;
                closestDistance = distance;
            }
        }

        return closestEnemy;
    }

    private IEnumerator CastSpell(GameObject enemy, int spellNumber)
    {
        castBar.SetActive(true);
        _animator.SetBool(animations[0], true);
        float startingLife = life;

        while (_castSlider.value < 100 && Vector3.Distance(transform.position, _nextPosition) < 0.1f && startingLife <= life && enemy != null)
        {
            _castSlider.value += stats.dexterity / (_selectedSpells[spellNumber].instanceLevel * _selectedSpells[spellNumber].spell.castDelayPerLevel) * Time.deltaTime * 15;
            Quaternion nextRotation = Quaternion.LookRotation(enemy.transform.position - transform.position);
            nextRotation.x = transform.rotation.x;
            this.transform.rotation = nextRotation;
            yield return null;
        }

        _animator.SetBool(animations[0], false);
        _castSlider.value = 0;
        castBar.SetActive(false);

        if (Vector3.Distance(transform.position, _nextPosition) > 0.1f || startingLife > life || enemy == null)
            yield break;

        _animator.SetTrigger(animations[1]);
        _selectedSpells[spellNumber].spell.level = _selectedSpells[spellNumber].instanceLevel;
        SpellController.Cast(_selectedSpells[spellNumber].spell, transform.position, enemy.transform, stats.intelligence);

        manaBar.value -= _selectedSpells[spellNumber].spell.manaPerLevel * _selectedSpells[spellNumber].instanceLevel;
    }
    #endregion

    private void Update()
    {
        RegenerateMana();
    }

    private void RegenerateMana()
    {
        if (manaBar.value < 100)
            manaBar.value += 0.02f * stats.intelligence;
    }
}
