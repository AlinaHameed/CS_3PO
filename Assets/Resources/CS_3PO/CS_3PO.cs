using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using Unity.MLAgents;
using Unity.MLAgents.Sensors;
using Unity.MLAgents.Actuators;

public class CS_3PO : CogsAgent
{ 

    protected int currStrategy = 1;   
                        // persueEnemy = 0
                        // agressiveMid = 1
                        // defensive = 2
                        // persueEnemyBase = 3
                        // retreat = 4
    protected bool rotatingL = false;
    protected bool moving = false;
    protected bool shooting = false;

    protected int carrying = 0;

    protected int ours = 0;
    protected int theirs = 0;

    protected int lead = 0;
    int unclaimed = 8;

    bool sigLead = false;
    bool sigLoss = false;

    int enemyCarrying = 0;

    float locationPenalty = 0;

    float enemyPenalty = 0;
    GameObject myBaseE;
    VectorSensor stratSensor;


    
    
    // ------------------BASIC MONOBEHAVIOR FUNCTIONS-------------------
    
    // Initialize values
    protected override void Start()
    {
        this.GetComponent<RayPerceptionSensorComponent3D>().RaysPerDirection = 30;
        stratSensor = new VectorSensor(4, "Goal", ObservationType.GoalSignal);
    
        //this.GetComponent<CogsAgent>().AddComponent<VectorSensor>();
      //  stratSensor = this.GetComponent<VectorSensorComponent>();
        // VectorSensorComponent stratSensor = new VectorSensor(4, "Goal", ObservationType.GoalSignal);
        base.Start();
        
        AssignBasicRewards();


    }

    // For actual actions in the environment (e.g. movement, shoot laser)
    // that is done continuously
    protected override void FixedUpdate() {
        updateStats();
        base.FixedUpdate();
        
       // UpdateIncentives(); //Can we put this here? If not it should work on action recieved
        LaserControl();
        // Movement based on DirToGo and RotateDir
        moveAgent(dirToGo, rotateDir);
    }


    
    // --------------------AGENT FUNCTIONS-------------------------

    // Get relevant information from the environment to effectively learn behavior
    // (i.e. - Collect inputs for Neural Network)
    public override void CollectObservations(VectorSensor sensor)
    {
        //--------------------------------------
        // ***** AGENT ACTIONS *******
        //--------------------------------------
        
        updateStats();
        

       // VectorSensor stratSensor = new VectorSensor(4, "Goal", ObservationType.GoalSignal);
       // stratSensor.AddOneHotObservation(currStrategy, 4);

        stratSensor.AddOneHotObservation(currStrategy, 4);


        sensor.AddOneHotObservation(currStrategy, 4);
        
        // shooting, moving, and left rotation booleans
        // sensor.AddObservation(moving);
        // sensor.AddObservation(shooting);
        // sensor.AddObservation(ours);




        //velocity in x and z axis 
        var localVelocity = transform.InverseTransformDirection(rBody.velocity);
        sensor.AddObservation(localVelocity.x);
        sensor.AddObservation(localVelocity.z);

        //current rotation
        var localRotation = transform.rotation;
        sensor.AddObservation(transform.rotation.y);

        //agent position and distance to base
        sensor.AddObservation(this.transform.localPosition);
        sensor.AddObservation(DistanceToBase());

        // Whether the agent is frozen
        sensor.AddObservation(IsFrozen());
        sensor.AddObservation(GetCarrying());
        //--------------------------------------
        // ***** WORLD-STATE INFO *******
        //--------------------------------------
        // Time remaning
        sensor.AddObservation(timer.GetComponent<Timer>().GetTimeRemaning());

        //score info


        // base's position
        //sensor.AddObservation(baseLocation.localPosition);
        //--------------------------------------
        // ***** ENEMYINFO *******
        //-------------------------------------
        // enemy position
        sensor.AddObservation(enemyCarrying);
        sensor.AddObservation(enemy.GetComponent<CogsAgent>().IsLaserOn());

        
        //enemy distance from agent

        sensor.AddObservation(enemy.GetComponent<CogsAgent>().IsFrozen());
        sensor.AddObservation(EnemyDistanceToBase());

        
        foreach (GameObject target in targets){
            //sensor.AddObservation(target.transform.localPosition);
            sensor.AddObservation(target.GetComponent<Target>().GetCarried());
            sensor.AddObservation(target.GetComponent<Target>().GetInBase());
        }
    }

    // For manual override of controls. This function will use keyboard presses to simulate output from your NN 
    public override void Heuristic(in ActionBuffers actionsOut)
    {
        var discreteActionsOut = actionsOut.DiscreteActions;
        discreteActionsOut[0] = 0; //Simulated NN output 0
        discreteActionsOut[1] = 0; //....................1
        discreteActionsOut[2] = 0; //....................2
        discreteActionsOut[3] = 0; //....................3

        //TODO-2: Uncomment this next line when implementing GoBackToBase();
        discreteActionsOut[4] = 0;
       
        if (Input.GetKey(KeyCode.UpArrow))
        {
            discreteActionsOut[0] = 1;
        }       
        if (Input.GetKey(KeyCode.DownArrow))
        {
            discreteActionsOut[0] = 2;
        }
        if (Input.GetKey(KeyCode.RightArrow))
        {
            discreteActionsOut[1] = 1;
        }
        if (Input.GetKey(KeyCode.LeftArrow))
        {
            //TODO-1: Using the above as examples, set the action out for the left arrow press
            discreteActionsOut[1] = 2;
        }
        

        //Shoot
        if (Input.GetKey(KeyCode.Space)){
            discreteActionsOut[2] = 1;
        }

        //GoToNearestTarget
        if (Input.GetKey(KeyCode.A)){
            discreteActionsOut[3] = 1;
        }


        //TODO-2: implement a keypress (your choice of key) for the output for GoBackToBase();
        if (Input.GetKey(KeyCode.Q)) {
            discreteActionsOut[4] = 1;
        }

        if (currStrategy == 1 && carrying < 3) {
            discreteActionsOut[4] = 1;
        }

        if (currStrategy == 4) {
            discreteActionsOut[4] = 3;
        }

        if (currStrategy == 1 && carrying >= 2) {
            discreteActionsOut[4] = 3;
        }

        if (currStrategy == 2)  {
            if (DistanceToBase() > 10) {
                discreteActionsOut[4] = 3;
            } else {
                discreteActionsOut[1] = 1;
                discreteActionsOut[2] = 1;
            }
            
        }
    }

    // What to do when an action is received (i.e. when the Brain gives the agent information about possible actions)
    public override void OnActionReceived(ActionBuffers actions)
    {   
        updateStats();
        UpdateStrategy();
        UseStrategy();

        int forwardAxis = (int)actions.DiscreteActions[0]; 
        if (forwardAxis == 1 || forwardAxis == 2) {
            moving = true;
        } else {
            moving = false;
        }

        //TODO-1: Set these variables to their appopriate item from the act list
        int rotateAxis = (int)actions.DiscreteActions[1]; 
        if (rotateAxis == 2) {
            rotatingL = true;
        } else {
            rotatingL = false;
        }

        int shootAxis = (int)actions.DiscreteActions[2]; 
        if (shootAxis == 1) {
            shooting = true;
        } else {
            shooting = false;
        }

        int goToTargetAxis = (int)actions.DiscreteActions[3];
        
        
        //TODO-2: Uncomment this next line and set it to the appropriate item from the act list
        int potentialAxis;
        int persueAxis = (int)actions.DiscreteActions[3];
        if (currStrategy == 1 && carrying < 3) {
            potentialAxis = 1;
            if (potentialAxis == persueAxis) {
                AddReward(0.01f);
            }
        } else if (currStrategy == 4) {
            potentialAxis = 3;
            persueAxis = 3;
            if (potentialAxis == persueAxis) {
                AddReward(0.01f);
            }
        } else if (currStrategy == 1 && carrying >= 2) {
            potentialAxis = 3;
            if (potentialAxis == persueAxis) {
                AddReward(0.01f);
            }
        } else if (currStrategy == 0) {
            potentialAxis = 2;
            if (potentialAxis == persueAxis) {
                AddReward(0.01f);
            }
        } else if (currStrategy == 3) {
            potentialAxis = 4;
            if (potentialAxis == persueAxis) {
                AddReward(0.01f);
            }

        }



          

        //TODO-2: Make sure to remember to add goToBaseAxis when working on that part!
        MovePlayer(forwardAxis, rotateAxis, shootAxis, goToTargetAxis, persueAxis);




    }


// ----------------------ONTRIGGER AND ONCOLLISION FUNCTIONS------------------------
    // Called when object collides with or trigger (similar to collide but without physics) other objects
    protected override void OnTriggerEnter(Collider collision)
    {

        // clear movement penalties
        if (collision.gameObject.CompareTag("HomeBase") && collision.gameObject.GetComponent<HomeBase>().team == GetTeam() && GetCarrying() >= 1 && (currStrategy == 1 || currStrategy == 4)) {
            AddReward(-1f*locationPenalty);
            locationPenalty = 0f;
        }

        if (collision.gameObject.CompareTag("HomeBase") && collision.gameObject.GetComponent<HomeBase>().team != GetTeam() && GetCarrying() < 1 && (currStrategy == 3)) {
            AddReward(-1f*locationPenalty);
            locationPenalty = 0f;
        }

        if (collision.gameObject.CompareTag("HomeBase") && collision.gameObject.GetComponent<HomeBase>().team == GetTeam())
        {
            //Provide reward proportioal to points taken by agent to homebase, punish staying in home base with targets
            int RetreatMultiplier = 1;

            if (currStrategy == 4) {
                RetreatMultiplier = 3;
            } 

            AddReward(GetCarrying() * 20f * RetreatMultiplier);
        }
        base.OnTriggerEnter(collision);
    }

    protected override void OnCollisionEnter(Collision collision) 
    
    {
        
        if (collision.gameObject.CompareTag("Target") && collision.gameObject.GetComponent<Target>().GetInBase() == enemy.GetComponent<CogsAgent>().GetTeam() &&  GetCarrying() <= 2 && !IsFrozen())
        {
            if (currStrategy == 3) {
                AddReward(20f - 5f*GetCarrying()); 
            } else {
                AddReward(-10f);
            }
            
        }

        //target is not in my base and 2 or less targets are being carried and I am not frozen
        if (collision.gameObject.CompareTag("Target") && collision.gameObject.GetComponent<Target>().GetInBase() != GetTeam() && collision.gameObject.GetComponent<Target>().GetCarried() <= 2 && !IsFrozen())
        {
            AddReward(5f); 
        }


        //target is not in my base and I am carrying more than 2 objects 
        if (collision.gameObject.CompareTag("Target") && collision.gameObject.GetComponent<Target>().GetInBase() != GetTeam() && GetCarrying() > 2 && !IsFrozen())
        {
            AddReward(-5f);
        }

        // //target is not in my base and I am carrying more than 2 objects 
        // if (collision.gameObject.CompareTag("Target") && collision.gameObject.GetComponent<Target>().GetInBase() != GetTeam() && GetCarrying() > 2 && !IsFrozen())
        // {
        //     AddReward(-0.5f);
        // }

        // //target is my base and I am carrying no objects 
        // if (collision.gameObject.CompareTag("Target") && collision.gameObject.GetComponent<Target>().GetInBase() == GetTeam() && GetCarrying() == 0 && !IsFrozen())
        // {
        //     AddReward(-2f);
        // }

        //agents hits the wall
        if (collision.gameObject.CompareTag("Wall"))
        {
            AddReward(-100f);
        }

        base.OnCollisionEnter(collision);
    }



    //  --------------------------HELPERS---------------------------- 
     private void AssignBasicRewards() 
     {
        rewardDict = new Dictionary<string, float>();

        // CS_3P0 behavior; Positive rewards
        rewardDict.Add("shooting-laser", -0.2f); //shooting laser -> +0.2 point (we want to constantly fire) (note from kylen; this originally had 0f as the assigned value, unsure if meant to be positive or negative)
        rewardDict.Add("hit-enemy", 2f); //sucessfully hit enemy -> +2 points

        // CS_3P0 behavior; Negative rewards
        rewardDict.Add("frozen", -5f); //frozen -> -1 point
        rewardDict.Add("dropped-one-target", -1f); //dropped one target -> -1 point
        rewardDict.Add("dropped-targets", -15f); //dropped multiple targets -> -2 point (note from kylen: slightly confused about this section of our code in general, would it be better to make the weight = to the number of targets dropped?)

    }

    
    private void MovePlayer(int forwardAxis, int rotateAxis, int shootAxis, int goToTargetAxis, int persueAxis)
    {
        dirToGo = Vector3.zero;
        rotateDir = Vector3.zero;

        Vector3 forward = transform.forward;
        Vector3 backward = -transform.forward;
        Vector3 right = transform.up;
        Vector3 left = -transform.up;


        //note: we should probably use a switch case form here instead of so many iff statements

        //fowardAxis: 
            // 0 -> do nothing
            // 1 -> go forward
            // 2 -> go backward
        if (forwardAxis == 0){
            //do nothing. This case is not necessary to include, it's only here to explicitly show what happens in case 0
        }
        if (forwardAxis == 1){
            dirToGo = forward;
        }
        if (forwardAxis == 2){
            //TODO-1: Tell your agent to go backward!
            dirToGo = backward;
            
        }

        //rotateAxis: 
            // 0 -> do nothing
            // 1 -> go right
            // 2 -> go left
        if (rotateAxis == 0){
            //do nothing
        }
        
        //TODO-1 : Implement the other cases for rotateDir
        if (rotateAxis == 1) {
            rotateDir = right;
        }
        if (rotateAxis == 2) {
            rotateDir = left;
        }

        //shoot
        if (shootAxis == 1){
            SetLaser(true);
        }
        else {
            SetLaser(false);
        }

        //go to the nearest target
        if (persueAxis == 1){
            GoToNearestTarget();
        }

        if (persueAxis == 2){
            persueEnemy();
        }

        //TODO-2: Implement the case for goToBaseAxis
        if (persueAxis == 3) {
            GoToBase();
        }

     
        if (persueAxis == 4) {
             GoToEnemyBase();
        }
        
        
    }

    // Go to home base
    private void GoToBase(){
        TurnAndGo(GetYAngle(myBase));
    }

    private void GoToEnemyBase(){
        
        GameObject EBase = GameObject.Find("Base " + enemy.GetComponent<CogsAgent>().GetTeam());
        TurnAndGo(GetYAngle(EBase));
    }

    // Go to the nearest target
    private void GoToNearestTarget(){
        GameObject target = GetNearestTarget();
        if (target != null){
            float rotation = GetYAngle(target);
            TurnAndGo(rotation);
        }        
    }

    // Rotate and go in specified direction
    private void TurnAndGo(float rotation){

        if (rotation < -175f || rotation > 175f) {
            dirToGo = -transform.forward;

        } else if (rotation < -130f || rotation > 130f) {
            if(rotation < -130f){
                rotateDir = -transform.up;
                dirToGo = -transform.forward;
            }
            if(rotation > 130f){
                rotateDir = transform.up;
                dirToGo = -transform.forward;
            }
        } else if (rotation < -50f || rotation > 50f) {
            if(rotation < -50f){
                rotateDir = transform.up;
                dirToGo = -transform.forward;
            }
            if(rotation > 50f){
                rotateDir = -transform.up;
                dirToGo = -transform.forward;
            }

        } else {

        if(rotation < -5f){
            rotateDir = transform.up;
            dirToGo = transform.forward;
        }
        else if (rotation > 5f){
            rotateDir = -transform.up;
            dirToGo = transform.forward;
        } else {
            dirToGo = transform.forward;
        }

    }
    }


    // return reference to nearest target
    protected GameObject GetNearestTarget(){
        float distance = 200;
        GameObject nearestTarget = null;
        foreach (var target in targets)
        {
            float currentDistance = Vector3.Distance(target.transform.localPosition, transform.localPosition);
            if (currentDistance < distance && target.GetComponent<Target>().GetCarried() == 0 && target.GetComponent<Target>().GetInBase() != team){
                distance = currentDistance;
                nearestTarget = target;
            }
        }
        return nearestTarget;
    }

    private float GetYAngle(GameObject target) {
        
       Vector3 targetDir = target.transform.position - transform.position;
       Vector3 forward = transform.forward;

      float angle = Vector3.SignedAngle(targetDir, forward, Vector3.up);
      return angle; 
        
    }



/// ------------- ADITIONAL HELPERS ---------------------
    protected float HomeWeight() {
        float bDist = DistanceToBase();
        return (-1f/10f)*bDist + 5f;

    }


    protected float GoHomePenalty() {
        float bDist = DistanceToBase();
        return -bDist - 1;

    }

    protected float GoToEBasePenalty() {
        float bDist = DistanceToEnemyBase();
        return -bDist - 1;

    }
    
    protected float GoToCenterPenalty() {
        float bDist = DistanceToBase();
        return (-1f/80f)*((bDist - 40)*(bDist - 40));

    }

    
    protected float KeepEnemyOutMultiplier() {
        float bDist = EnemyDistanceToBase();
        return (-100f/((bDist*bDist)+50f)) +0.1f;

    }

    protected void updateStats() {


      // theirs = myBaseE.GetCaptured();
        myBaseE = GameObject.Find("Base " + enemy.GetComponent<CogsAgent>().GetTeam());
        
        ours = myBase.GetComponent<HomeBase>().GetCaptured();
        theirs = myBaseE.GetComponent<HomeBase>().GetCaptured();
        
        carrying = this.GetComponent<CogsAgent>().GetCarrying();
        enemyCarrying = this.GetComponent<CogsAgent>().GetCarrying();

        lead = ours - theirs;
        unclaimed =  targets.Length - (ours + theirs);
        sigLead = ours > theirs + unclaimed + 2;
        sigLoss = theirs > ours + unclaimed + 2;

        handleScorePenalty();

    }
    protected float DistanceToEnemy(){
        return Vector3.Distance(enemy.transform.localPosition, transform.localPosition);
    }

    protected float DistanceToEnemyBase(){

        GameObject myBaseE = GameObject.Find("Base " + enemy.GetComponent<CogsAgent>().GetTeam());
        return Vector3.Distance(myBaseE.transform.localPosition, transform.localPosition);
    }

    protected float EnemyDistanceToBase(){
        return Vector3.Distance(baseLocation.localPosition, enemy.transform.localPosition);
    }


    private void WinningStrategies() {
        bool endgame = timer.GetComponent<Timer>().GetTimeRemaning() < 30;
        
        //bool sigLead = sigLead;
        if (enemy.GetComponent<CogsAgent>().GetCarrying() > this.GetComponent<CogsAgent>().GetCarrying() + lead + unclaimed) {
                currStrategy = 0;
        } else if (GetCarrying() > 1) {
            currStrategy = 4;
        } else {
            if (unclaimed > lead) {
                currStrategy = 1;
            } else if (theirs + unclaimed > ours) {
                if (!endgame) {
                    currStrategy = 1;
                } else {
                    currStrategy = 2; // unsure here
                }
            } else {
                currStrategy = 2;
            }
        }






    }


    private void TiedStrategies() { 
        bool endgame = timer.GetComponent<Timer>().GetTimeRemaning() < 20;
        bool earlygame = timer.GetComponent<Timer>().GetTimeRemaning() > 80;

    if (GetCarrying() > enemy.GetComponent<CogsAgent>().GetCarrying()) {
        currStrategy = 4;
    } else {
        if (unclaimed > 0 || earlygame) {
            currStrategy = 1;
        } else {
            currStrategy = 3;
        }
    }
       
    }
        

    private void LosingStrategies() { 

        bool endgame = timer.GetComponent<Timer>().GetTimeRemaning() < 30;
        int enemyLead = lead*-1;
        //bool sigLead = sigLead;
        if (this.GetComponent<CogsAgent>().GetCarrying() > enemyLead) {
                currStrategy = 4;
        } else {
            if (unclaimed > enemyLead) {
                currStrategy = 1;
            } else if (unclaimed == enemyLead) {
                if (!endgame) {
                    currStrategy = 1;
                } else {
                    currStrategy = 3; // unsure here
                }
            } else {
                currStrategy = 3;
            }
        }


    }  
        

    private void UpdateStrategy() {

        if (lead > 0) {
            WinningStrategies();
        } else if (lead == 0) {
            TiedStrategies();
        } else {
            LosingStrategies();
        }

    }

    private void persueEnemy() {

        TurnAndGo(GetYAngle(enemy));

        if (DistanceToEnemy() < 16)
        {   
            TurnAndGo(GetYAngle(enemy));
        }

        if (DistanceToEnemy() < 13)
        {   
            TurnAndGo(GetYAngle(enemy));
            SetLaser(true);
        }


        if (enemy.GetComponent<CogsAgent>().IsFrozen())
        {
            GoToNearestTarget();
        }

    }

    private void handleScorePenalty() {
        AddReward(0.5f * ours);
        AddReward(-0.7f * theirs);
        if (enemyCarrying > ours + lead && enemyCarrying > 0) {
            AddReward(-1f);
        }

        if (sigLoss) {AddReward(-2f);}
        if (sigLead) {AddReward(2f);}
    }


    private void addLocPenalty(float p) {
        AddReward(p);
        locationPenalty = locationPenalty + p;
    }

    private void addEnemyPenality(float p) {
        AddReward(p);
        enemyPenalty = enemyPenalty + p;
    }



    private void UseStrategy()
    {   
        int tempEnemyCarry = enemy.GetComponent<CogsAgent>().GetCarrying();
        switch (currStrategy)
        {
            case 0:
                persueEnemy();
                break;
            case 1:
                if (this.GetComponent<CogsAgent>().GetCarrying() < 1)
                {
                    addLocPenalty(0.001f * GoToCenterPenalty());
                    GoToNearestTarget();
                }
                else
                {      
                    addLocPenalty(0.01f * GoHomePenalty() *  GetCarrying());
                }

                if (shooting)
                {
                    AddReward(-0.005f);
                }

                if (!moving)
                {
                    AddReward(-0.025f);
                }
                break;

            case 2:
                
                addEnemyPenality(0.1f * KeepEnemyOutMultiplier());     

                if (EnemyDistanceToBase() > 50) {
                    enemyPenalty = 0;
                }

                if (shooting) {
                    AddReward(0.0002f);
                }      

                if (DistanceToBase() > EnemyDistanceToBase())
                {
                    if (DistanceToEnemy() < 16)
                    {   
                        TurnAndGo(GetYAngle(enemy));
                        AddReward(0.00002f);
                        SetLaser(true);
                    }
                    
                    if (enemy.GetComponent<CogsAgent>().IsFrozen())
                    {
                        AddReward(-0.0005f * enemyPenalty);
                        enemyPenalty = enemyPenalty / 1.5f;
                        GoToNearestTarget();
                    }
                }            
                break;
            case 3:
                addLocPenalty(0.00025f*GoToEBasePenalty());
                break;
            case 4:
                addLocPenalty(0.01f*GoHomePenalty());
                GoToBase();
                break;
        }


    }
}