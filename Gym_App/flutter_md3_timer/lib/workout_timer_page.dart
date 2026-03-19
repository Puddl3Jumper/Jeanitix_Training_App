import 'dart:async';
import 'package:flutter/material.dart';

enum TimerMode { activeSet, rest }
enum MuscleGroup { chest, back, shoulder, arm, leg }

class WorkoutTimerPage extends StatefulWidget {
  const WorkoutTimerPage({super.key});

  @override
  State<WorkoutTimerPage> createState() => _WorkoutTimerPageState();
}

class _WorkoutTimerPageState extends State<WorkoutTimerPage> {
  Timer? _ticker;
  TimerMode _mode = TimerMode.activeSet;
  MuscleGroup _group = MuscleGroup.chest;
  bool _running = false;

  int _activeSeconds = 0;
  int _restSeconds = 60;
  int _restRemaining = 60;
  int _durationSeconds = 0;

  final List<_TimerLogItem> _todayLog = [];
  int _setNumber = 1;

  @override
  void dispose() {
    _ticker?.cancel();
    super.dispose();
  }

  String get _dateLabel {
    final now = DateTime.now();
    return '${now.year}-${now.month.toString().padLeft(2, '0')}-${now.day.toString().padLeft(2, '0')}';
  }

  void _startPause() {
    if (_running) {
      _stopTickerAndLogIfNeeded();
    } else {
      _startTicker();
    }
  }

  void _startTicker() {
    _ticker?.cancel();
    setState(() => _running = true);

    _ticker = Timer.periodic(const Duration(seconds: 1), (_) {
      setState(() {
        _durationSeconds++;
        if (_mode == TimerMode.activeSet) {
          _activeSeconds++;
          return;
        }

        if (_restRemaining > 0) _restRemaining--;
        if (_restRemaining == 0) {
          _running = false;
          _ticker?.cancel();
        }
      });
    });
  }

  void _stopTickerAndLogIfNeeded() {
    _ticker?.cancel();
    _running = false;

    if (_mode == TimerMode.activeSet && _activeSeconds > 0) {
      _todayLog.add(_TimerLogItem(
        group: _groupLabel(_group),
        exerciseName: 'TBD',
        setNumber: _setNumber,
        setSeconds: _activeSeconds,
        restSeconds: _restSeconds,
        timestamp: DateTime.now(),
      ));
      _setNumber++;
      _activeSeconds = 0;
    }

    setState(() {});
  }

  void _switchToActive() {
    _ticker?.cancel();
    setState(() {
      _running = false;
      _mode = TimerMode.activeSet;
      _restRemaining = _restSeconds;
    });
  }

  void _switchToRest() {
    _stopTickerAndLogIfNeeded();
    setState(() {
      _mode = TimerMode.rest;
      _restRemaining = _restSeconds;
    });
  }

  void _skipRest() {
    _ticker?.cancel();
    setState(() {
      _running = false;
      _mode = TimerMode.activeSet;
      _restRemaining = _restSeconds;
    });
  }

  String _groupLabel(MuscleGroup g) {
    switch (g) {
      case MuscleGroup.chest:
        return 'Chest';
      case MuscleGroup.back:
        return 'Back';
      case MuscleGroup.shoulder:
        return 'Shoulder';
      case MuscleGroup.arm:
        return 'Arm';
      case MuscleGroup.leg:
        return 'Leg';
    }
  }

  String _fmt(int sec) {
    final m = sec ~/ 60;
    final s = sec % 60;
    return '${m.toString().padLeft(2, '0')}:${s.toString().padLeft(2, '0')}';
  }

  void _showLogSheet() {
    showModalBottomSheet<void>(
      context: context,
      showDragHandle: true,
      builder: (context) {
        return SafeArea(
          child: Padding(
            padding: const EdgeInsets.all(16),
            child: _todayLog.isEmpty
                ? const Text('No timer logs for today.')
                : ListView.separated(
                    itemCount: _todayLog.length,
                    separatorBuilder: (_, __) => const Divider(),
                    itemBuilder: (context, index) {
                      final item = _todayLog[index];
                      return ListTile(
                        title: Text('${item.group} • ${item.exerciseName} • Set ${item.setNumber}'),
                        subtitle: Text('Active ${_fmt(item.setSeconds)} • Rest ${_fmt(item.restSeconds)}'),
                        trailing: Text('${item.timestamp.hour.toString().padLeft(2, '0')}:${item.timestamp.minute.toString().padLeft(2, '0')}'),
                      );
                    },
                  ),
          ),
        );
      },
    );
  }

  @override
  Widget build(BuildContext context) {
    final restProgress = (_restRemaining / (_restSeconds == 0 ? 1 : _restSeconds)).clamp(0, 1).toDouble();

    return Scaffold(
      appBar: AppBar(
        title: const Text('Training'),
        centerTitle: false,
        bottom: PreferredSize(
          preferredSize: const Size.fromHeight(44),
          child: Padding(
            padding: const EdgeInsets.fromLTRB(16, 0, 16, 12),
            child: Row(
              children: [
                Expanded(child: Text('Duration ${_fmt(_durationSeconds)}', style: Theme.of(context).textTheme.bodyMedium)),
                Text(_dateLabel, style: Theme.of(context).textTheme.bodyMedium),
              ],
            ),
          ),
        ),
      ),
      floatingActionButton: FloatingActionButton.extended(
        onPressed: _startPause,
        icon: Icon(_running ? Icons.pause : Icons.play_arrow),
        label: Text(_running ? 'Pause' : 'Start'),
      ),
      floatingActionButtonLocation: FloatingActionButtonLocation.centerDocked,
      bottomNavigationBar: BottomAppBar(
        child: Padding(
          padding: const EdgeInsets.symmetric(horizontal: 16, vertical: 10),
          child: ElevatedButton.icon(
            onPressed: () {},
            icon: const Icon(Icons.add),
            label: const Text('Add Exercise / Circuit'),
          ),
        ),
      ),
      body: SafeArea(
        child: ListView(
          padding: const EdgeInsets.all(16),
          children: [
            SegmentedButton<MuscleGroup>(
              showSelectedIcon: false,
              segments: const [
                ButtonSegment(value: MuscleGroup.chest, label: Text('Chest')),
                ButtonSegment(value: MuscleGroup.back, label: Text('Back')),
                ButtonSegment(value: MuscleGroup.shoulder, label: Text('Shoulder')),
                ButtonSegment(value: MuscleGroup.arm, label: Text('Arm')),
                ButtonSegment(value: MuscleGroup.leg, label: Text('Leg')),
              ],
              selected: {_group},
              onSelectionChanged: (value) {
                setState(() {
                  _group = value.first;
                  _restSeconds = _group == MuscleGroup.leg ? 90 : 60;
                  _restRemaining = _restSeconds;
                });
              },
            ),
            const SizedBox(height: 16),
            ElevatedCard(
              shape: RoundedRectangleBorder(borderRadius: BorderRadius.circular(24)),
              child: Padding(
                padding: const EdgeInsets.all(16),
                child: Column(
                  crossAxisAlignment: CrossAxisAlignment.stretch,
                  children: [
                    Text(_mode == TimerMode.activeSet ? 'Active Set' : 'Rest', style: Theme.of(context).textTheme.titleLarge),
                    const SizedBox(height: 8),
                    Text(_fmt(_mode == TimerMode.activeSet ? _activeSeconds : _restRemaining), style: Theme.of(context).textTheme.displaySmall),
                    const SizedBox(height: 16),
                    Text('Rest Time: ${_restSeconds}s'),
                    Slider(
                      value: _restSeconds.toDouble(),
                      min: 30,
                      max: 180,
                      divisions: 10,
                      label: '${_restSeconds}s',
                      onChanged: (v) {
                        setState(() {
                          _restSeconds = v.round();
                          if (_mode == TimerMode.rest && !_running) {
                            _restRemaining = _restSeconds;
                          }
                        });
                      },
                    ),
                    const SizedBox(height: 12),
                    Center(
                      child: SizedBox(
                        width: 150,
                        height: 150,
                        child: AnimatedCircularProgressIndicator(value: restProgress),
                      ),
                    ),
                    if (_mode == TimerMode.rest && _restRemaining <= 10 && _restRemaining > 0)
                      Padding(
                        padding: const EdgeInsets.only(top: 8),
                        child: Center(
                          child: Text('$_restRemaining' 's left!', style: Theme.of(context).textTheme.titleMedium),
                        ),
                      ),
                    const SizedBox(height: 16),
                    FilledButton(
                      onPressed: _startPause,
                      child: Text(_running ? 'Pause' : 'Start'),
                    ),
                    const SizedBox(height: 8),
                    FilledButton(
                      onPressed: _mode == TimerMode.rest ? _switchToActive : _switchToRest,
                      child: Text(_mode == TimerMode.rest ? 'Switch to Active Set' : 'Switch to Rest'),
                    ),
                    const SizedBox(height: 8),
                    if (_mode == TimerMode.rest)
                      FilledButton(
                        onPressed: _skipRest,
                        child: const Text('Skip Rest'),
                      ),
                    const SizedBox(height: 8),
                    ElevatedButton(
                      onPressed: _showLogSheet,
                      child: const Text('View Log'),
                    ),
                  ],
                ),
              ),
            ),
          ],
        ),
      ),
    );
  }
}

class AnimatedCircularProgressIndicator extends StatelessWidget {
  final double value;
  const AnimatedCircularProgressIndicator({super.key, required this.value});

  @override
  Widget build(BuildContext context) {
    return TweenAnimationBuilder<double>(
      tween: Tween<double>(begin: 1, end: value),
      duration: const Duration(milliseconds: 350),
      builder: (context, v, _) {
        return Stack(
          fit: StackFit.expand,
          children: [
            CircularProgressIndicator(
              value: v,
              strokeWidth: 12,
            ),
            Center(
              child: Text('${(v * 100).round()}%'),
            ),
          ],
        );
      },
    );
  }
}

class _TimerLogItem {
  final String group;
  final String exerciseName;
  final int setNumber;
  final int setSeconds;
  final int restSeconds;
  final DateTime timestamp;

  const _TimerLogItem({
    required this.group,
    required this.exerciseName,
    required this.setNumber,
    required this.setSeconds,
    required this.restSeconds,
    required this.timestamp,
  });
}
