//
//  CLXNativeTextView.mm
//  Native iOS text rendering for Unity - supports emoji display
//

#import <UIKit/UIKit.h>
#import <Foundation/Foundation.h>

// Native text view controller for displaying logs with emoji support
@interface CLXNativeLogsViewController : UIViewController <UITableViewDataSource, UITableViewDelegate>
@property (nonatomic, strong) UITableView *tableView;
@property (nonatomic, strong) NSMutableArray<NSDictionary *> *logs;
@property (nonatomic, strong) UIView *headerView;
@end

@implementation CLXNativeLogsViewController

- (void)viewDidLoad {
    [super viewDidLoad];
    
    self.view.backgroundColor = [UIColor whiteColor];
    self.logs = [NSMutableArray array];
    
    // Header
    self.headerView = [[UIView alloc] init];
    self.headerView.backgroundColor = [UIColor colorWithRed:0.96 green:0.96 blue:0.96 alpha:1.0];
    self.headerView.translatesAutoresizingMaskIntoConstraints = NO;
    [self.view addSubview:self.headerView];
    
    // Title
    UILabel *titleLabel = [[UILabel alloc] init];
    titleLabel.text = @"Event Logs";
    titleLabel.font = [UIFont boldSystemFontOfSize:18];
    titleLabel.textAlignment = NSTextAlignmentCenter;
    titleLabel.translatesAutoresizingMaskIntoConstraints = NO;
    [self.headerView addSubview:titleLabel];
    
    // Close button
    UIButton *closeButton = [UIButton buttonWithType:UIButtonTypeSystem];
    [closeButton setTitle:@"Close" forState:UIControlStateNormal];
    closeButton.titleLabel.font = [UIFont systemFontOfSize:17];
    [closeButton addTarget:self action:@selector(closeButtonTapped) forControlEvents:UIControlEventTouchUpInside];
    closeButton.translatesAutoresizingMaskIntoConstraints = NO;
    [self.headerView addSubview:closeButton];
    
    // Clear button
    UIButton *clearButton = [UIButton buttonWithType:UIButtonTypeSystem];
    [clearButton setTitle:@"Clear" forState:UIControlStateNormal];
    [clearButton setTitleColor:[UIColor systemRedColor] forState:UIControlStateNormal];
    clearButton.titleLabel.font = [UIFont systemFontOfSize:17];
    [clearButton addTarget:self action:@selector(clearButtonTapped) forControlEvents:UIControlEventTouchUpInside];
    clearButton.translatesAutoresizingMaskIntoConstraints = NO;
    [self.headerView addSubview:clearButton];
    
    // Table view
    self.tableView = [[UITableView alloc] initWithFrame:CGRectZero style:UITableViewStylePlain];
    self.tableView.dataSource = self;
    self.tableView.delegate = self;
    self.tableView.separatorStyle = UITableViewCellSeparatorStyleNone;
    self.tableView.backgroundColor = [UIColor whiteColor];
    self.tableView.translatesAutoresizingMaskIntoConstraints = NO;
    [self.tableView registerClass:[UITableViewCell class] forCellReuseIdentifier:@"LogCell"];
    [self.view addSubview:self.tableView];
    
    // Layout constraints
    UILayoutGuide *safe = self.view.safeAreaLayoutGuide;
    [NSLayoutConstraint activateConstraints:@[
        [self.headerView.topAnchor constraintEqualToAnchor:safe.topAnchor],
        [self.headerView.leadingAnchor constraintEqualToAnchor:self.view.leadingAnchor],
        [self.headerView.trailingAnchor constraintEqualToAnchor:self.view.trailingAnchor],
        [self.headerView.heightAnchor constraintEqualToConstant:50],
        
        [titleLabel.centerXAnchor constraintEqualToAnchor:self.headerView.centerXAnchor],
        [titleLabel.centerYAnchor constraintEqualToAnchor:self.headerView.centerYAnchor],
        
        [closeButton.trailingAnchor constraintEqualToAnchor:self.headerView.trailingAnchor constant:-16],
        [closeButton.centerYAnchor constraintEqualToAnchor:self.headerView.centerYAnchor],
        
        [clearButton.leadingAnchor constraintEqualToAnchor:self.headerView.leadingAnchor constant:16],
        [clearButton.centerYAnchor constraintEqualToAnchor:self.headerView.centerYAnchor],
        
        [self.tableView.topAnchor constraintEqualToAnchor:self.headerView.bottomAnchor],
        [self.tableView.leadingAnchor constraintEqualToAnchor:self.view.leadingAnchor],
        [self.tableView.trailingAnchor constraintEqualToAnchor:self.view.trailingAnchor],
        [self.tableView.bottomAnchor constraintEqualToAnchor:self.view.bottomAnchor],
    ]];
}

- (void)closeButtonTapped {
    [self dismissViewControllerAnimated:YES completion:nil];
}

- (void)clearButtonTapped {
    [self.logs removeAllObjects];
    [self.tableView reloadData];
}

- (void)addLogWithTimestamp:(NSString *)timestamp message:(NSString *)message {
    [self.logs addObject:@{@"timestamp": timestamp, @"message": message}];
    
    // Scroll to bottom
    dispatch_async(dispatch_get_main_queue(), ^{
        [self.tableView reloadData];
        if (self.logs.count > 0) {
            NSIndexPath *lastRow = [NSIndexPath indexPathForRow:self.logs.count - 1 inSection:0];
            [self.tableView scrollToRowAtIndexPath:lastRow atScrollPosition:UITableViewScrollPositionBottom animated:NO];
        }
    });
}

#pragma mark - UITableViewDataSource

- (NSInteger)tableView:(UITableView *)tableView numberOfRowsInSection:(NSInteger)section {
    return self.logs.count;
}

- (UITableViewCell *)tableView:(UITableView *)tableView cellForRowAtIndexPath:(NSIndexPath *)indexPath {
    UITableViewCell *cell = [tableView dequeueReusableCellWithIdentifier:@"LogCell" forIndexPath:indexPath];
    
    // Clear existing subviews
    for (UIView *subview in cell.contentView.subviews) {
        [subview removeFromSuperview];
    }
    
    NSDictionary *log = self.logs[indexPath.row];
    
    // Card background
    UIView *cardView = [[UIView alloc] init];
    cardView.backgroundColor = [UIColor colorWithRed:0.96 green:0.96 blue:0.96 alpha:1.0];
    cardView.layer.cornerRadius = 8;
    cardView.translatesAutoresizingMaskIntoConstraints = NO;
    [cell.contentView addSubview:cardView];
    
    // Timestamp label - blue
    UILabel *timestampLabel = [[UILabel alloc] init];
    timestampLabel.text = log[@"timestamp"];
    timestampLabel.font = [UIFont systemFontOfSize:10];
    timestampLabel.textColor = [UIColor systemBlueColor];
    timestampLabel.translatesAutoresizingMaskIntoConstraints = NO;
    [cardView addSubview:timestampLabel];
    
    // Message label - supports emojis!
    UILabel *messageLabel = [[UILabel alloc] init];
    messageLabel.text = log[@"message"];
    messageLabel.font = [UIFont systemFontOfSize:12];
    messageLabel.textColor = [UIColor blackColor];
    messageLabel.numberOfLines = 0;
    messageLabel.translatesAutoresizingMaskIntoConstraints = NO;
    [cardView addSubview:messageLabel];
    
    [NSLayoutConstraint activateConstraints:@[
        [cardView.topAnchor constraintEqualToAnchor:cell.contentView.topAnchor constant:4],
        [cardView.leadingAnchor constraintEqualToAnchor:cell.contentView.leadingAnchor constant:12],
        [cardView.trailingAnchor constraintEqualToAnchor:cell.contentView.trailingAnchor constant:-12],
        [cardView.bottomAnchor constraintEqualToAnchor:cell.contentView.bottomAnchor constant:-4],
        
        [timestampLabel.topAnchor constraintEqualToAnchor:cardView.topAnchor constant:8],
        [timestampLabel.leadingAnchor constraintEqualToAnchor:cardView.leadingAnchor constant:8],
        [timestampLabel.trailingAnchor constraintEqualToAnchor:cardView.trailingAnchor constant:-8],
        
        [messageLabel.topAnchor constraintEqualToAnchor:timestampLabel.bottomAnchor constant:4],
        [messageLabel.leadingAnchor constraintEqualToAnchor:cardView.leadingAnchor constant:8],
        [messageLabel.trailingAnchor constraintEqualToAnchor:cardView.trailingAnchor constant:-8],
        [messageLabel.bottomAnchor constraintEqualToAnchor:cardView.bottomAnchor constant:-8],
    ]];
    
    cell.selectionStyle = UITableViewCellSelectionStyleNone;
    cell.backgroundColor = [UIColor whiteColor];
    
    return cell;
}

- (CGFloat)tableView:(UITableView *)tableView estimatedHeightForRowAtIndexPath:(NSIndexPath *)indexPath {
    return 80;
}

- (CGFloat)tableView:(UITableView *)tableView heightForRowAtIndexPath:(NSIndexPath *)indexPath {
    return UITableViewAutomaticDimension;
}

@end

// Singleton to hold the logs view controller
static CLXNativeLogsViewController *_logsViewController = nil;
static NSMutableArray<NSDictionary *> *_pendingLogs = nil;

#pragma mark - C Interface for Unity

extern "C" {

void _CLXNativeLogsShow() {
    dispatch_async(dispatch_get_main_queue(), ^{
        if (_logsViewController == nil) {
            _logsViewController = [[CLXNativeLogsViewController alloc] init];
        }
        
        // Add any pending logs
        if (_pendingLogs) {
            for (NSDictionary *log in _pendingLogs) {
                [_logsViewController addLogWithTimestamp:log[@"timestamp"] message:log[@"message"]];
            }
            [_pendingLogs removeAllObjects];
        }
        
        UIViewController *rootVC = [UIApplication sharedApplication].keyWindow.rootViewController;
        
        // Find the topmost presented view controller
        while (rootVC.presentedViewController) {
            rootVC = rootVC.presentedViewController;
        }
        
        _logsViewController.modalPresentationStyle = UIModalPresentationFullScreen;
        [rootVC presentViewController:_logsViewController animated:YES completion:nil];
    });
}

void _CLXNativeLogsHide() {
    dispatch_async(dispatch_get_main_queue(), ^{
        if (_logsViewController) {
            [_logsViewController dismissViewControllerAnimated:YES completion:nil];
        }
    });
}

void _CLXNativeLogsAddEntry(const char* timestamp, const char* message) {
    NSString *ts = timestamp ? [NSString stringWithUTF8String:timestamp] : @"";
    NSString *msg = message ? [NSString stringWithUTF8String:message] : @"";
    
    dispatch_async(dispatch_get_main_queue(), ^{
        if (_logsViewController) {
            [_logsViewController addLogWithTimestamp:ts message:msg];
        } else {
            // Store for later if view controller doesn't exist yet
            if (_pendingLogs == nil) {
                _pendingLogs = [NSMutableArray array];
            }
            [_pendingLogs addObject:@{@"timestamp": ts, @"message": msg}];
        }
    });
}

void _CLXNativeLogsClear() {
    dispatch_async(dispatch_get_main_queue(), ^{
        if (_logsViewController) {
            [_logsViewController clearButtonTapped];
        }
        [_pendingLogs removeAllObjects];
    });
}

bool _CLXNativeLogsIsShowing() {
    return _logsViewController != nil && _logsViewController.presentingViewController != nil;
}

}
