using System;
using System.Collections;
using System.Collections.Generic;
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
    private InputAction _lookAction;
    private InputAction _mouseWheelPressAction;
    
    private bool _isRotating = false;
    [SerializeField] private float rotationSpeed;
    [SerializeField] private Transform cameraFollow;
    [SerializeField] private Transform rotationTarget;

    private Animator _animator;

    [SerializeField] private List<string> animations = new();
    private Vector3 _nextPosition;

    [SerializeField] private float movSpeed;
    [SerializeField] private Slider manaBar;
    [SerializeField] private GameObject castBar;
    private Slider _castSlider;

    private List<ISpells> _selectedSpells;
    public Stats stats;

    public event Action OnStatsPressed;
    public event Action OnSpellUIPressed;

    private Coroutine rotationCoroutine;

    #region Setup
    private void Awake()
    {
        _inputs = GetComponent<PlayerInput>();
        _animator = GetComponent<Animator>();

        _rightClickAction = _inputs.actions["RightClick"];
        _spellsAction = _inputs.actions["Spells"];
        _spellsUIAction = _inputs.actions["SpellsUI"];
        _statsPanelAction = _inputs.actions["Stats"];
        _lookAction = _inputs.actions["Look"];
        _mouseWheelPressAction = _inputs.actions["MouseWheelPress"];

        Cursor.visible = true;
    }

    private new void Start()
    {
        base.Start();
        _castSlider = castBar.GetComponent<Slider>();
        stats = Stats.NewStats();
        stats.dexterity = 8;
        _nextPosition = transform.position;
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
        _mouseWheelPressAction.performed += _ => _isRotating = true;
        _mouseWheelPressAction.canceled += _ => _isRotating = false;
    }

    private void OnDisable()
    {
        _inputs.DeactivateInput();
        _rightClickAction.performed -= GoToPosition;
        _spellsAction.performed -= HandleSpellCasting;
        _statsPanelAction.performed -= _ => OnStatsPressed?.Invoke();
        _spellsUIAction.performed -= _ => OnSpellUIPressed?.Invoke();
        _mouseWheelPressAction.performed -= _ => _isRotating = true;
        _mouseWheelPressAction.canceled -= _ => _isRotating = false;
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
            if(rotationCoroutine != null)
                StopCoroutine(rotationCoroutine);
            rotationCoroutine = StartCoroutine(SmoothRotateTowards(_nextPosition));
        }
        else
            _animator.SetBool(animations[2], false);
    }

    private IEnumerator SmoothRotateTowards(Vector3 targetPosition)
    {
        float rotationSpeed = 5f;
        Quaternion initialRotation = rotationTarget.localRotation;
        Quaternion targetRotation = Quaternion.LookRotation(targetPosition - transform.position);

        while (Quaternion.Angle(rotationTarget.localRotation, targetRotation) > 0.1f)
        {
            rotationTarget.localRotation = Quaternion.Slerp(rotationTarget.localRotation, targetRotation, Time.deltaTime * rotationSpeed);
            yield return null;
        }

        rotationTarget.localRotation = targetRotation;
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
                if (CursorManager.instance.GetCurrentCursor() == CursorManager.CursorTypes.SpellSelect)
                    StopAllCoroutines();
                else if (castBar.activeSelf)
                    return;
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
            && _selectedSpells[spellNumber].Prefab != null
            && manaBar.value > _selectedSpells[spellNumber].ManaPerLevel * _selectedSpells[spellNumber].Level
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
        
        Character enemyCharacter;
        enemy.TryGetComponent<Character>(out enemyCharacter);

        while (_castSlider.value < 100 && Vector3.Distance(transform.position, _nextPosition) < 0.1f && startingLife <= life && enemy != null)
        {
            this.ShowCastingCircle();
            if(enemyCharacter != null) 
                enemyCharacter.BeingTargeted(true);
            _castSlider.value += stats.dexterity / (_selectedSpells[spellNumber].Level * _selectedSpells[spellNumber].CastDelayPerLevel) * Time.deltaTime * 15;
            Quaternion nextRotation = Quaternion.LookRotation(enemy.transform.position - transform.position);
            nextRotation.x = transform.rotation.x;
            if(nextRotation != Quaternion.identity) 
                rotationTarget.localRotation = nextRotation;
            yield return null;
        }
        this.HideCastingCircle();
        if (enemyCharacter != null)
            enemyCharacter.BeingTargeted(false);

        _animator.SetBool(animations[0], false);
        _castSlider.value = 0;
        castBar.SetActive(false);

        if (Vector3.Distance(transform.position, _nextPosition) > 0.1f || startingLife > life || enemy == null)
            yield break;

        //_animator.SetTrigger(animations[1]);
        _selectedSpells[spellNumber].Level = _selectedSpells[spellNumber].Level;
        SpellController.Cast(_selectedSpells[spellNumber], rotationTarget, enemy.transform, stats.intelligence);

        manaBar.value -= _selectedSpells[spellNumber].ManaPerLevel * _selectedSpells[spellNumber].Level;
    }
    #endregion

    private void Update()
    {
        RegenerateMana();

        if (_isRotating && cameraFollow != null)
        {
            Vector2 mouseDelta = _lookAction.ReadValue<Vector2>();

            float rotationX = mouseDelta.x;
            cameraFollow.Rotate(0, rotationX, 0, Space.World);
        }
    }

    private void RegenerateMana()
    {
        if (manaBar.value < 100)
            manaBar.value += 0.02f * stats.intelligence;
    }
}
